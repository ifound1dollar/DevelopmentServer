using ENet;
using System.Collections.Concurrent;

namespace ENetServer
{
    public class NetworkManager
    {
        #region Singleton Stuff

        private static readonly NetworkManager instance = new();

        static NetworkManager()
        {
            // Static constructor allows for thread-safe singleton usage.
            // See: https://csharpindepth.com/articles/singleton, fourth option.
        }
        private NetworkManager()
        {
            // Default constructor
        }

        public static NetworkManager Instance { get { return instance; } }

        #endregion



        // Thread-safe queues for communicating game data between main and serialization/intermediate threads.
        private ConcurrentQueue<GameOutObject> GameOutQueue { get; } = new();
        private ConcurrentQueue<GameInObject> GameInQueue { get; } = new();

        // Thread-safe queues for communicating network data between network and serialization/intermediate threads.
        private ConcurrentQueue<NetworkSendObject> NetSendQueue { get; } = new();
        private ConcurrentQueue<NetworkRecvObject> NetRecvQueue { get; } = new();

        // These are nullable so they can be manually initialized in Setup().
        private SerializeWorker? serializeWorker;
        private NetworkWorker? networkWorker;

        /// <summary>
        /// Thread-safe Dictionary of all connected clients in form ID:Client.
        /// </summary>
        private ConcurrentDictionary<uint, Client> Clients { get; set; } = new();



        /// <summary>
        /// Sets up NetworkManager, initializing thread workers.
        /// </summary>
        public void Setup()
        {
            serializeWorker = new SerializeWorker();
            networkWorker = new NetworkWorker();
        }

        /// <summary>
        /// Starts separate threads for serialization/intermediate work and networking work.
        /// </summary>
        public void StartThreadedOperations()
        {
            // Starts each threaded operation (one for serialization, one for ENet) here.
            serializeWorker?.StartThread();
            networkWorker?.StartThread();
        }

        /// <summary>
        /// Stops serialization/intermediate and network threads.
        /// </summary>
        public void StopThreadedOperations()
        {
            // Stops each threaded operation gracefully (could do this a couple of ways, but one could
            //  simply be to effectively copy the current Server implementation, NOT PREFERABLE).
            networkWorker?.StopThread();
            serializeWorker?.StopThread();

            // NOTE: NetworkWorker should be stopped first because disconnecting clients will enqueue
            //  disconnect events that should be handled by the serialization thread.
        }



        public void SendGameDataObject(GameOutObject gameOutObject)
        {
            // Queues the passed-in GameOutObject to be processed and sent.
            GameOutQueue.Enqueue(gameOutObject);
        }

        public GameInObject? ReadOneGameDataObject()
        {
            // Try to dequeue one, returning the result if successful or null if failed.
            return GameInQueue.TryDequeue(out GameInObject result) ? result : null;
        }

        public Client? GetConnectedClient(uint clientId)
        {
            Clients.TryGetValue(clientId, out Client? client);
            return client;
        }



        #region Worker Classes

        private class SerializeWorker
        {
            private readonly Thread thread;
            private volatile bool shouldExit = false;

            private readonly ConcurrentQueue<GameOutObject> gameOutQueue = NetworkManager.Instance.GameOutQueue;
            private readonly ConcurrentQueue<NetworkSendObject> netSendQueue = NetworkManager.Instance.NetSendQueue;
            private readonly ConcurrentQueue<GameInObject> gameInQueue = NetworkManager.Instance.GameInQueue;
            private readonly ConcurrentQueue<NetworkRecvObject> netRecvQueue = NetworkManager.Instance.NetRecvQueue;
            private readonly ConcurrentDictionary<uint, Client> clientsDict = NetworkManager.Instance.Clients;

            internal SerializeWorker()
            {
                // Thread will call the Run() method.
                thread = new(this.Run);
            }

            private void Run()
            {
                while (!shouldExit)
                {
                    // Reads from game out queue, serializes, adds to network send queue.
                    GameOutTasks();

                    // Reads from network receive queue, deserializes, adds to game in queue.
                    NetReceiveTasks();
                }
            }

            /// <summary>
            /// Handles game out tasks - read from game out queue, operate on data (serialize), add to network send queue.
            /// </summary>
            private void GameOutTasks()
            {
                // Loop until queue is empty.
                while (!gameOutQueue.IsEmpty)
                {
                    // Try to dequeue item from serializeQueue, operating on the item if successful.
                    if (!gameOutQueue.TryDequeue(out GameOutObject gameOutObject)) break;

                    // Operate based on send type.
                    switch (gameOutObject.sendType)
                    {
                        case SendType.DISCONNECT_ONE:
                            {
                                NetworkSendObject sendObject = NetHelpers.CreateNetworkSendObject(
                                    gameOutObject.peerId, new Packet(), SendType.DISCONNECT_ONE);
                                netSendQueue.Enqueue(sendObject);
                                break;
                            }
                        case SendType.DISCONNECT_ALL:
                            {
                                NetworkSendObject sendObject = NetHelpers.CreateNetworkSendObject(
                                    gameOutObject.peerId, new Packet(), SendType.DISCONNECT_ALL);
                                netSendQueue.Enqueue(sendObject);
                                break;
                            }
                        case SendType.MESSAGE_ONE:
                            {
                                //operate on and SERIALIZE gameDataObject
                                string sendString = NetHelpers.FormatStringForSend(gameOutObject.tempDataString);
                                Packet packet = NetHelpers.CreatePacketFromUTF8String(sendString);

                                // Create NetworkDataObject after serializing, then enqueue for network send.
                                NetworkSendObject dataObject = NetHelpers.CreateNetworkSendObject(
                                    gameOutObject.peerId, packet, SendType.MESSAGE_ONE);
                                netSendQueue.Enqueue(dataObject);
                                break;
                            }
                        case SendType.MESSAGE_ALL:
                            {
                                //operate on and SERIALIZE gameDataObject
                                string sendString = NetHelpers.FormatStringForSend(gameOutObject.tempDataString);
                                Packet packet = NetHelpers.CreatePacketFromUTF8String(sendString);

                                // Create NetworkDataObject after serializing, then enqueue for network send.
                                NetworkSendObject dataObject = NetHelpers.CreateNetworkSendObject(
                                    gameOutObject.peerId, packet, SendType.MESSAGE_ALL);
                                netSendQueue.Enqueue(dataObject);
                                break;
                            }
                        case SendType.MESSAGE_ALLEXCEPT:
                            {
                                //operate on and SERIALIZE gameDataObject
                                string sendString = NetHelpers.FormatStringForSend(gameOutObject.tempDataString);
                                Packet packet = NetHelpers.CreatePacketFromUTF8String(sendString);

                                // Create NetworkDataObject after serializing, then enqueue for network send.
                                NetworkSendObject dataObject = NetHelpers.CreateNetworkSendObject(
                                    gameOutObject.peerId, packet, SendType.MESSAGE_ALLEXCEPT);
                                netSendQueue.Enqueue(dataObject);
                                break;
                            }
                    }
                }
            }

            /// <summary>
            /// Handles net receive tasks - read from network receive queue, operate on data (deserialize), add to game in queue.
            /// </summary>
            private void NetReceiveTasks()
            {
                // Loop until net receive queue is empty.
                while (!netRecvQueue.IsEmpty)
                {
                    // Try to dequeue item from netRecvQueue, operating on the item if successful.
                    if (!netRecvQueue.TryDequeue(out NetworkRecvObject netReceiveData)) break;

                    // Operate based on receive type.
                    switch (netReceiveData.recvType)
                    {
                        case RecvType.CONNECT:
                            {
                                // Add new connection to Clients dict and log connection.
                                Client client = new(netReceiveData.peerId, netReceiveData.peerIP, netReceiveData.peerPort);
                                clientsDict.TryAdd(client.ID, client);

                                Console.WriteLine("[CONNECT] Client connected - ID: " + client.ID +
                                    ", Address: " + client.IP + ":" + client.Port);

                                break;
                            }
                        case RecvType.DISCONNECT:
                            {
                                // Remove now-disconnected client from Clients dict and log disconnect.
                                clientsDict.Remove(netReceiveData.peerId, out _);   // Discard out parameter

                                Console.WriteLine("[DISCONNECT] Client disconnected - ID: " + netReceiveData.peerId +
                                    ", Address: " + netReceiveData.peerIP + ":" + netReceiveData.peerPort);

                                break;
                            }
                        case RecvType.TIMEOUT:
                            {
                                // Remove now-disconnected client from Clients dict and log disconnect.
                                clientsDict.Remove(netReceiveData.peerId, out _);   // Discard out parameter

                                Console.WriteLine("[TIMEOUT] Client timeout - ID: " + netReceiveData.peerId +
                                    ", Address: " + netReceiveData.peerIP + ":" + netReceiveData.peerPort);

                                break;
                            }
                        case RecvType.MESSAGE:
                            {
                                //operate on and DESERIALIZE netReceiveData
                                string recvString = NetHelpers.FormatStringFromReceive(netReceiveData.bytes);

                                // Create GameDataObject after deserializing, then enqueue for game/main thread reading.
                                GameInObject dataObject = NetHelpers.CreateGameInObject(netReceiveData.peerId, recvString);
                                gameInQueue.Enqueue(dataObject);



                                // ----- BELOW IS ENTIRELY TEMPORARY, SIMULATING GAME THREAD READING FROM GAME IN QUEUE -----
                                if (!gameInQueue.TryDequeue(out GameInObject tempGameObject)) break;
                                // Output incoming data.
                                Console.WriteLine("[MESSAGE] Packet received from - ID: " + tempGameObject.peerId +
                                                    ", Message: " + tempGameObject.tempDataString);



                                break;
                            }
                    }
                }
            }



            /// <summary>
            /// Starts the worker thread, beginning serialization/deserialization operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                thread.Start();
                Console.WriteLine("[STARTUP] Starting serialization thread.");
            }

            /// <summary>
            /// Stops the worker thread, waiting for server to shut down before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Sets shouldExit to true, which will gracefully exit the threaded loop.
                shouldExit = true;

                // Wait for the Server.Run() function to return, then join the thread (BLOCKS).
                Console.WriteLine("[EXIT] Waiting for serialization thread to stop...");
                thread.Join();
                Console.WriteLine("[EXIT] Serialization thread stopped successfully.");
            }


        }

        private class NetworkWorker
        {
            private readonly Thread thread;
            private readonly Server server;
            private volatile bool shouldExit = false;

            private readonly ConcurrentQueue<NetworkSendObject> netSendQueue = NetworkManager.Instance.NetSendQueue;
            private ConcurrentQueue<NetworkRecvObject> netRecvQueue = NetworkManager.Instance.NetRecvQueue;

            internal NetworkWorker()
            {
                // Thread will call the Run() method.
                thread = new(this.Run);

                // INITIALIZE ENET SERVER, BUT DO NOT CREATE SERVER YET.
                server = new Server();
            }

            private void Run()
            {
                // Start server, opening the host up for incoming connections and messages.
                server.Start();

                while (!shouldExit)
                {
                    // Reads from network send queue, then queues ENet operations.
                    NetSendTasks();

                    // Handles ENet events, runs host service, and adds to network receive queue.
                    server.DoENetTasks(ref netRecvQueue);
                }

                // Stop server, waiting at least 3 seconds for all clients to be disconnected properly.
                server.Stop(ref netRecvQueue);
            }

            /// <summary>
            /// Handles net send tasks - read from network send queue, do ENet tasks (send, disconnect, etc.) via server.
            /// </summary>
            private void NetSendTasks()
            {
                // Loop until network send queue is empty.
                while (!netSendQueue.IsEmpty)
                {
                    // Try to dequeue item from serializeQueue, operating on the item if successful.
                    if (!netSendQueue.TryDequeue(out NetworkSendObject netSendObject)) break;

                    // Operate based on send type.
                    switch (netSendObject.sendType)
                    {
                        case SendType.DISCONNECT_ONE:
                            {
                                server.QueueDisconnectOne(netSendObject.peerId);
                                break;
                            }
                        case SendType.DISCONNECT_ALL:
                            {
                                server.QueueDisconnectAll();
                                break;
                            }
                        case SendType.MESSAGE_ONE:
                            {
                                server.QueueSendOne(netSendObject.peerId, netSendObject.packet);
                                break;
                            }
                        case SendType.MESSAGE_ALL:
                            {
                                server.QueueSendAll(netSendObject.packet);
                                break;
                            }
                        case SendType.MESSAGE_ALLEXCEPT:
                            {
                                server.QueueSendAllExcept(netSendObject.peerId, netSendObject.packet);
                                break;
                            }
                    }
                }
            }



            /// <summary>
            /// Starts the worker thread, beginning server operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                thread.Start();
                Console.WriteLine("[STARTUP] Server host started listening on {0}:{1}",
                    server.GetAddress().GetIP(), server.GetAddress().Port);
            }

            /// <summary>
            /// Stops the worker thread, waiting for server to shut down before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Sets shouldExit to true, which will gracefully exit the threaded loop.
                shouldExit = true;

                // Wait for the Server.Run() function to return, then join the thread (BLOCKS).
                Console.WriteLine("[EXIT] Waiting for server to shut down...");
                thread.Join();
                Console.WriteLine("[EXIT] Server shut down successfully.");
            }
        }

        #endregion

        #region Data structs

        public struct GameOutObject(uint peerId, string tempDataString, SendType sendType)
        {
            public uint peerId = peerId;
            public string tempDataString = tempDataString;
            public SendType sendType = sendType;
        }

        public struct GameInObject(uint peerId, string tempDataString)
        {
            public uint peerId = peerId;
            public string tempDataString = tempDataString;
        }

        internal struct NetworkSendObject(uint peerId, Packet packet, SendType sendType)
        {
            public uint peerId = peerId;
            public Packet packet = packet;
            public SendType sendType = sendType;
        }

        internal struct NetworkRecvObject(uint peerId, string peerIP, ushort peerPort, byte[] bytes, RecvType recvType)
        {
            public uint peerId = peerId;
            public string peerIP = peerIP;
            public ushort peerPort = peerPort;
            public byte[] bytes = bytes;
            public RecvType recvType = recvType;
        }

        #endregion

        public enum SendType { MESSAGE_ONE, MESSAGE_ALL, MESSAGE_ALLEXCEPT, DISCONNECT_ONE, DISCONNECT_ALL }
        internal enum RecvType { CONNECT, DISCONNECT, TIMEOUT, MESSAGE }

    }
}
