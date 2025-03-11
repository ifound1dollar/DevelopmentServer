using ENet;
using ENetServer.NetObjects;
using ENetServer.Management;
using System;
using System.Collections.Concurrent;
using static ENetServer.NetHelpers;
using ENetServer.NetObjects.DataObjects;

namespace ENetServer.Serialize
{
    /// <summary>
    /// Encapsulates all serialization/deserialization operations within this class.
    /// </summary>
    internal class Serializer
    {
        // QUEUE REFERENCES
        private readonly ConcurrentQueue<GameSendObject> gameSendQueue;
        private readonly ConcurrentQueue<NetworkSendObject> netSendQueue;
        private readonly ConcurrentQueue<GameRecvObject> gameRecvQueue;
        private readonly ConcurrentQueue<NetworkRecvObject> netRecvQueue;
        private readonly ConcurrentDictionary<uint, Connection> connectionsDict;

        /// <summary>
        /// Constructs a Serializer object with references to game AND network concurrent queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Serializer(ConcurrentQueue<GameSendObject> gameSendQueue,
            ConcurrentQueue<NetworkSendObject> netSendQueue,
            ConcurrentQueue<GameRecvObject> gameRecvQueue,
            ConcurrentQueue<NetworkRecvObject> netRecvQueue,
            ConcurrentDictionary<uint, Connection> connectionsDict)
        {
            this.gameSendQueue = gameSendQueue;
            this.netSendQueue = netSendQueue;
            this.gameRecvQueue = gameRecvQueue;
            this.netRecvQueue = netRecvQueue;
            this.connectionsDict = connectionsDict;
        }



        /// <summary>
        /// Handles game send tasks - read from game out queue, operate on data (serialize), add to network send queue.
        /// </summary>
        internal void DoGameToNetTasks()
        {
            // Loop until queue is empty.
            while (!gameSendQueue.IsEmpty)
            {
                // Try to dequeue item from serializeQueue, operating on the item if successful.
                if (!gameSendQueue.TryDequeue(out GameSendObject? gameSendObject)) break;

                // Operate based on send type.
                switch (gameSendObject.SendType)
                {
                    case SendType.Disconnect_One:
                        {
                            SendDisconnectOne(gameSendObject);
                            break;
                        }
                    case SendType.Disconnect_All:
                        {
                            SendDisconnectAll();
                            break;
                        }
                    case SendType.Message_One:
                        {
                            SendMessageOne(gameSendObject);
                            break;
                        }
                    case SendType.Message_All:
                        {
                            SendMessageAll(gameSendObject);
                            break;
                        }
                    case SendType.Message_AllExcept:
                        {
                            SendMessageAllExcept(gameSendObject);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Handles game receive tasks - read from network receive queue, operate on data (deserialize), add to game in queue.
        /// </summary>
        internal void DoNetToGameTasks()
        {
            // Loop until net receive queue is empty.
            while (!netRecvQueue.IsEmpty)
            {
                // Try to dequeue item from netRecvQueue, operating on the item if successful.
                if (!netRecvQueue.TryDequeue(out NetworkRecvObject? netReceiveData)) break;

                // Operate based on receive type.
                switch (netReceiveData.RecvType)
                {
                    case RecvType.Connect:
                        {
                            ReceiveConnect(netReceiveData);
                            break;
                        }
                    case RecvType.Disconnect:
                        {
                            ReceiveDisconnect(netReceiveData);
                            break;
                        }
                    case RecvType.Timeout:
                        {
                            ReceiveTimeout(netReceiveData);
                            break;
                        }
                    case RecvType.Message:
                        {
                            ReceiveMessage(netReceiveData);
                            break;
                        }
                }
            }
        }



        #region Send Methods

        private void SendDisconnectOne(GameSendObject gameObject)
        {
            NetworkSendObject dataObject = NetworkSendObject.Factory.CreateDisconnectOne(gameObject.PeerID);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendDisconnectAll()
        {
            NetworkSendObject dataObject = NetworkSendObject.Factory.CreateDisconnectAll();
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageOne(GameSendObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameObject.GameDataObject);
            if (bytes == null)
            {
                Console.WriteLine("[ERROR] Cannot serialize a null GameDataObject. Aborting.");
                return;
            }

            NetworkSendObject dataObject = NetworkSendObject.Factory.CreateMessageOne(gameObject.PeerID, bytes);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageAll(GameSendObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameObject.GameDataObject);
            if (bytes == null)
            {
                Console.WriteLine("[ERROR] Cannot serialize a null GameDataObject. Aborting.");
                return;
            }

            NetworkSendObject dataObject = NetworkSendObject.Factory.CreateMessageAll(bytes);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageAllExcept(GameSendObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[]. If empty array, log failure and return.
            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameObject.GameDataObject);
            if (bytes == null)
            {
                Console.WriteLine("[ERROR] Cannot serialize a null GameDataObject. Aborting.");
                return;
            }

            NetworkSendObject dataObject = NetworkSendObject.Factory.CreateMessageAllExcept(gameObject.PeerID, bytes);
            netSendQueue.Enqueue(dataObject);
        }

        #endregion

        #region Receive Methods

        private void ReceiveConnect(NetworkRecvObject recvObject)
        {
            // Create new Connection and try to add to Connections dict. If failed to add, disconnect and log failure.
            Connection connection = new(recvObject.PeerID, recvObject.PeerIP, recvObject.PeerPort);
            if (!connectionsDict.TryAdd(connection.ID, connection))
            {
                // Create and enqueue disconnect request.
                NetworkSendObject dataObject = NetworkSendObject.Factory.CreateDisconnectOne(recvObject.PeerID);
                netSendQueue.Enqueue(dataObject);

                // Log connection failure.
                Console.WriteLine("[ERROR] New connection from " + connection.IP + ":" + connection.Port
                    + " could not be added to Connections list. Aborting.");
                return;
            }
            
            // Else connection was successful.
            Console.WriteLine("[CONNECT] Client connected - ID: " + connection.ID +
                ", Address: " + connection.IP + ":" + connection.Port);
        }

        private void ReceiveDisconnect(NetworkRecvObject recvObject)
        {
            // Remove now-disconnected client from Connections dict and log disconnect.
            connectionsDict.Remove(recvObject.PeerID, out _);   // Discard out parameter

            Console.WriteLine("[DISCONNECT] Client disconnected - ID: " + recvObject.PeerID +
                ", Address: " + recvObject.PeerIP + ":" + recvObject.PeerPort);
        }

        private void ReceiveTimeout(NetworkRecvObject recvObject)
        {
            // Remove now-disconnected client from Connections dict and log disconnect.
            connectionsDict.Remove(recvObject.PeerID, out _);   // Discard out parameter

            Console.WriteLine("[TIMEOUT] Client timeout - ID: " + recvObject.PeerID +
                ", Address: " + recvObject.PeerIP + ":" + recvObject.PeerPort);

        }

        private void ReceiveMessage(NetworkRecvObject recvObject)
        {
            // Attempt to deserialize received byte[] into GameDataObject. Log error and return if failure.
            GameDataObject? gameDataObject = GameDataObject.DeserializeGameDataObject(recvObject.Bytes);
            if (gameDataObject == null)
            {
                Console.WriteLine("[ERROR] Failed to deserialize data received from peer ID {0}. Discarding.", recvObject.PeerID);
                return;
            }

            // Create GameRecvObject after deserializing, then enqueue for game/main thread reading.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromMessage(recvObject.PeerID, gameDataObject);
            gameRecvQueue.Enqueue(gameRecvObject);



            /// ----- BELOW IS ENTIRELY TEMPORARY, SIMULATING GAME THREAD READING FROM GAME IN QUEUE -----
            /// TODO: ELIMINATE THIS WHEN IMPLEMENTED IN ENGINE
            if (!gameRecvQueue.TryDequeue(out GameRecvObject? tempRecvObject)) return;

            // Output incoming data.
            Console.WriteLine("[MESSAGE] Packet received from - ID: " + tempRecvObject.PeerID +
                                ", Message: " + tempRecvObject.GameDataObject?.GetDescription());



        }

        #endregion

        //TODO: MODIFY THIS CLASS TO ENSURE SAFE CLIENT OPERATION (CONNECTIONSDICT REFERENCE AND ADDING/REMOVING)

    }
}
