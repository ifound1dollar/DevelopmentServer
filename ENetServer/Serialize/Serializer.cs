using ENet;
using ENetServer.NetObjects;
using ENetServer.Network;
using System;
using System.Collections.Concurrent;
using static ENetServer.NetStatics;
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
        private readonly ConcurrentQueue<NetSendObject> netSendQueue;
        private readonly ConcurrentQueue<GameRecvObject> gameRecvQueue;
        private readonly ConcurrentQueue<NetRecvObject> netRecvQueue;

        /// <summary>
        /// Constructs a Serializer object with references to game AND network concurrent queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Serializer(ConcurrentQueue<GameSendObject> gameSendQueue,
            ConcurrentQueue<NetSendObject> netSendQueue,
            ConcurrentQueue<GameRecvObject> gameRecvQueue,
            ConcurrentQueue<NetRecvObject> netRecvQueue)
        {
            this.gameSendQueue = gameSendQueue;
            this.netSendQueue = netSendQueue;
            this.gameRecvQueue = gameRecvQueue;
            this.netRecvQueue = netRecvQueue;
        }



        /// <summary>
        /// Handles game send tasks - read from game out queue, operate on data (serialize), add to network send queue.
        /// </summary>
        internal void DoGameToNetTasks()
        {
            // Store number of elements when this method is initially called, then dequeue that many.
            // This will prevent an infinite loop that could be encountered if items were being added
            //  to the queue as fast or faster than they were processed, which would cause the thread
            //  to get stuck dequeuing and never run ENet events.

            int queueCount = gameSendQueue.Count;
            for (int i = 0; i < queueCount; i++)
            {
                // Try to dequeue item from serializeQueue, operating on the item if successful.
                if (!gameSendQueue.TryDequeue(out GameSendObject? gameSendObject)) break;

                // Operate based on send type.
                switch (gameSendObject.SendType)
                {
                    case SendType.Connect_One:
                        {
                            SendConnectOne(gameSendObject);
                            break;
                        }
                    case SendType.Disconnect_One:
                        {
                            SendDisconnectOne(gameSendObject);
                            break;
                        }
                    case SendType.Disconnect_Many:
                        {
                            SendDisconnectMany(gameSendObject);
                            break;
                        }
                    case SendType.Disconnect_All:
                        {
                            SendDisconnectAll(gameSendObject);
                            break;
                        }
                    case SendType.Message_One:
                        {
                            SendMessageOne(gameSendObject);
                            break;
                        }
                    case SendType.Message_Many:
                        {
                            SendMessageMany(gameSendObject);
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
                    case SendType.TestSend:
                        {
                            // TODO: REMOVE THIS TEST CASE
                            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameSendObject.GameDataObject);
                            if (bytes != null)
                            {
                                NetSendObject netSendObject = NetSendObject.Factory.CreateTestSend(
                                    gameSendObject.PeerParams, bytes);
                                netSendQueue.Enqueue(netSendObject);
                            }
                            
                            break;
                        }
                    // DO NOTHING FOR DEFAULT CASE
                }

                /* - outside of switch case, inside while loop - */
            }
        }

        /// <summary>
        /// Handles game receive tasks - read from network receive queue, operate on data (deserialize), add to game in queue.
        /// </summary>
        internal void DoNetToGameTasks()
        {
            // Store number of elements when this method is initially called, then dequeue that many.
            // This will prevent an infinite loop that could be encountered if items were being added
            //  to the queue as fast or faster than they were processed, which would cause the thread
            //  to get stuck dequeuing and never run ENet events.

            int queueCount = netRecvQueue.Count;
            for (int i = 0; i < queueCount; i++)
            {
                // Try to dequeue item from netRecvQueue, operating on the item if successful.
                if (!netRecvQueue.TryDequeue(out NetRecvObject? netReceiveObject)) break;

                // Operate based on receive type.
                switch (netReceiveObject.RecvType)
                {
                    case RecvType.Connect:
                        {
                            ReceiveConnect(netReceiveObject);
                            break;
                        }
                    case RecvType.Disconnect:
                        {
                            ReceiveDisconnect(netReceiveObject);
                            break;
                        }
                    case RecvType.Timeout:
                        {
                            ReceiveTimeout(netReceiveObject);
                            break;
                        }
                    case RecvType.Message:
                        {
                            ReceiveMessage(netReceiveObject);
                            break;
                        }
                    case RecvType.TestRecv:
                        {
                            // TODO: REMOVE THIS TEST CASE
                            GameDataObject? gameDataObject = GameDataObject.DeserializeGameDataObject(netReceiveObject.Bytes);
                            if (gameDataObject != null)
                            {
                                GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromTestRecv(
                                    netReceiveObject.Connection, gameDataObject);
                                gameRecvQueue.Enqueue(gameRecvObject);
                            }
                            break;
                        }
                    // DO NOTHING FOR DEFAULT CASE
                }

                /* - outside of switch case, inside while loop - */
            }
        }



        #region Send Methods

        private void SendConnectOne(GameSendObject gameObject)
        {
            NetSendObject netSendObject = NetSendObject.Factory.CreateConnectOne(gameObject.PeerParams);
            netSendQueue.Enqueue(netSendObject);
        }

        private void SendDisconnectOne(GameSendObject gameObject)
        {
            NetSendObject dataObject = NetSendObject.Factory.CreateDisconnectOne(gameObject.PeerParams);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendDisconnectMany(GameSendObject gameObject)
        {
            NetSendObject dataObject = NetSendObject.Factory.CreateDisconnectMany(gameObject.PeerParams);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendDisconnectAll(GameSendObject gameObject)
        {
            NetSendObject dataObject = NetSendObject.Factory.CreateDisconnectAll(gameObject.PeerParams);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageOne(GameSendObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameObject.GameDataObject);
            if (bytes == null)
            {
                Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                return;
            }

            NetSendObject dataObject = NetSendObject.Factory.CreateMessageOne(gameObject.PeerParams, bytes);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageMany(GameSendObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameObject.GameDataObject);
            if (bytes == null)
            {
                Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                return;
            }

            NetSendObject dataObject = NetSendObject.Factory.CreateMessageMany(gameObject.PeerParams, bytes);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageAll(GameSendObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameObject.GameDataObject);
            if (bytes == null)
            {
                Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                return;
            }

            NetSendObject dataObject = NetSendObject.Factory.CreateMessageAll(gameObject.PeerParams, bytes);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageAllExcept(GameSendObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[]. If empty array, log failure and return.
            byte[]? bytes = GameDataObject.SerializeGameDataObject(gameObject.GameDataObject);
            if (bytes == null)
            {
                Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                return;
            }

            NetSendObject dataObject = NetSendObject.Factory.CreateMessageAllExcept(gameObject.PeerParams, bytes);
            netSendQueue.Enqueue(dataObject);
        }

        #endregion

        #region Receive Methods

        private void ReceiveConnect(NetRecvObject recvObject)
        {
            // Simply pass incoming connect object to main/game thread to be handled there.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromConnect(recvObject.Connection);
            gameRecvQueue.Enqueue(gameRecvObject);
        }

        private void ReceiveDisconnect(NetRecvObject recvObject)
        {
            // Simply pass incoming disconnect object to main/game thread to be handled there.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromDisconnect(recvObject.Connection);
            gameRecvQueue.Enqueue(gameRecvObject);
        }

        private void ReceiveTimeout(NetRecvObject recvObject)
        {
            // Simply pass incoming timeout object to main/game thread to be handled there.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromTimeout(recvObject.Connection);
            gameRecvQueue.Enqueue(gameRecvObject);
        }

        private void ReceiveMessage(NetRecvObject recvObject)
        {
            // Attempt to deserialize received byte[] into GameDataObject. Log error and return if failure.
            GameDataObject? gameDataObject = GameDataObject.DeserializeGameDataObject(recvObject.Bytes);
            if (gameDataObject == null)
            {
                Console.WriteLine("[SERIALIZER_ERROR] Failed to deserialize data received from peer ID {0}. Discarding.",
                    recvObject.Connection.ID);
                return;
            }

            // Create GameRecvObject after deserializing, then enqueue for game/main thread handling.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromMessage(recvObject.Connection, gameDataObject);
            gameRecvQueue.Enqueue(gameRecvObject);
        }

        #endregion

    }
}
