using ENet;
using ENetServer.DataObjects;
using ENetServer.Management;
using ENetServer.Serialize;
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
        private ConcurrentQueue<GameOutDataObject> GameOutQueue { get; } = new();
        private ConcurrentQueue<GameInDataObject> GameInQueue { get; } = new();

        // Thread-safe queues for communicating network data between network and serialization/intermediate threads.
        private ConcurrentQueue<NetworkSendDataObject> NetSendQueue { get; } = new();
        private ConcurrentQueue<NetworkRecvDataObject> NetRecvQueue { get; } = new();

        /// <summary>
        /// Thread-safe Dictionary of all connected clients in form ID:Connection.
        /// </summary>
        private ConcurrentDictionary<uint, Connection> Connections { get; set; } = new();

        // These are nullable so they can be manually initialized in Setup().
        private SerializeWorker? serializeWorker;
        private NetworkWorker? networkWorker;



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



        public void SendGameDataObject(GameOutDataObject gameOutObject)
        {
            // Queues the passed-in GameOutObject to be processed and sent.
            GameOutQueue.Enqueue(gameOutObject);
        }

        public GameInDataObject? ReadOneGameDataObject()
        {
            // Try to dequeue one, returning the result if successful or null if failed.
            return GameInQueue.TryDequeue(out GameInDataObject? result) ? result : null;
        }

        public Connection? GetConnectedClient(uint clientId)
        {
            Connections.TryGetValue(clientId, out Connection? client);
            return client;
        }



        #region Worker Classes

        /// <summary>
        /// This class is responsible for managing the Serialization/Deserialization thread.
        /// </summary>
        private class SerializeWorker
        {
            private readonly Thread thread;
            private readonly Serializer serializer;
            private volatile bool shouldExit = false;

            internal SerializeWorker()
            {
                // Thread will call the Run() method.
                thread = new(Run);

                // Startup serializer (but do not begin running).
                serializer = new Serializer(Instance.GameOutQueue, Instance.NetSendQueue,
                    Instance.GameInQueue, Instance.NetRecvQueue, Instance.Connections);
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
            /// Stops the worker thread, waiting for any remaining work to finish before joining and returning.
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

            /// <summary>
            /// Performs serialization/deserialization operations continuously until 'shouldExit' is true.
            /// </summary>
            private void Run()
            {
                while (!shouldExit)
                {
                    // Reads from game out queue, serializes, adds to network send queue.
                    serializer.DoGameToNetTasks();

                    // Reads from network receive queue, deserializes, adds to game in queue.
                    serializer.DoNetToGameTasks();
                }
            }
        }

        /// <summary>
        /// This class is responsible for managing the Network/ENet thread.
        /// </summary>
        private class NetworkWorker
        {
            private readonly Thread thread;
            private readonly Server server;
            private volatile bool shouldExit = false;

            internal NetworkWorker()
            {
                // Thread will call the Run() method.
                thread = new(Run);

                // INITIALIZE ENET SERVER, BUT DO NOT CREATE SERVER YET.
                server = new Server(Instance.NetSendQueue, Instance.NetRecvQueue);
                server.SetHostParameters(); // SET HOST PARAMETERS HERE (HAVE DEFAULT VALUES)
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

            /// <summary>
            /// Creates server, then performs ENet networking tasks until 'shouldExit' is true, then stops server.
            /// </summary>
            private void Run()
            {
                // Start server, opening the host up for incoming connections and messages.
                server.Start();

                while (!shouldExit)
                {
                    // Reads from network send queue, then queues ENet operations.
                    server.DoNetSendTasks();

                    // Handles ENet events, runs host service, and adds to network receive queue.
                    server.DoNetReceiveTasks();
                }

                // Stop server, waiting at least 3 seconds for all clients to be disconnected properly.
                server.Stop();
            }
        }

        #endregion
    }
}
