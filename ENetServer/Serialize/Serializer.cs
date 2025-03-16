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
                if (!netRecvQueue.TryDequeue(out NetRecvObject? netReceiveData)) break;

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
            NetSendObject dataObject = NetSendObject.Factory.CreateDisconnectOne(gameObject.PeerID);
            netSendQueue.Enqueue(dataObject);
        }

        private void SendDisconnectAll()
        {
            NetSendObject dataObject = NetSendObject.Factory.CreateDisconnectAll();
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

            NetSendObject dataObject = NetSendObject.Factory.CreateMessageOne(gameObject.PeerID, bytes);
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

            NetSendObject dataObject = NetSendObject.Factory.CreateMessageAll(bytes);
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

            NetSendObject dataObject = NetSendObject.Factory.CreateMessageAllExcept(gameObject.PeerID, bytes);
            netSendQueue.Enqueue(dataObject);
        }

        #endregion

        #region Receive Methods

        private void ReceiveConnect(NetRecvObject recvObject)
        {
            // Simply pass incoming connect object to main/game thread to be handled there.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromConnect(recvObject.Connection);
        }

        private void ReceiveDisconnect(NetRecvObject recvObject)
        {
            // Simply pass incoming disconnect object to main/game thread to be handled there.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromDisconnect(recvObject.Connection);
        }

        private void ReceiveTimeout(NetRecvObject recvObject)
        {
            // Simply pass incoming timeout object to main/game thread to be handled there.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromTimeout(recvObject.Connection);
        }

        private void ReceiveMessage(NetRecvObject recvObject)
        {
            // Attempt to deserialize received byte[] into GameDataObject. Log error and return if failure.
            GameDataObject? gameDataObject = GameDataObject.DeserializeGameDataObject(recvObject.Bytes);
            if (gameDataObject == null)
            {
                Console.WriteLine("[ERROR] Failed to deserialize data received from peer ID {0}. Discarding.",
                    recvObject.Connection.ID);
                return;
            }

            // Create GameRecvObject after deserializing, then enqueue for game/main thread handling.
            GameRecvObject gameRecvObject = GameRecvObject.Factory.CreateFromMessage(recvObject.Connection, gameDataObject);
            gameRecvQueue.Enqueue(gameRecvObject);
        }

        #endregion

        /// <summary>
        /// Simulates the main/game thread dequeuing GameRecvObjects. This is a temporary development method.
        /// </summary>
        internal void SimulateMainThreadDequeue()
        {
            // Loop until game receive queue is empty.
            while (!gameRecvQueue.IsEmpty)
            {
                // Try to dequeue item from gameRecvQueue, operating on the item if successful.
                if (!gameRecvQueue.TryDequeue(out GameRecvObject? gameReceiveData)) break;

                // Operate based on receive type.
                switch (gameReceiveData.RecvType)
                {
                    case RecvType.Connect:
                        {
                            Console.WriteLine("[CONNECT] Client connected - ID: " + gameReceiveData.Connection.ID +
                                ", Address: " + gameReceiveData.Connection.IP + ":" + gameReceiveData.Connection.Port);
                            break;
                        }
                    case RecvType.Disconnect:
                        {
                            Console.WriteLine("[DISCONNECT] Client disconnected - ID: " + gameReceiveData.Connection.ID +
                                ", Address: " + gameReceiveData.Connection.IP + ":" + gameReceiveData.Connection.Port);
                            break;
                        }
                    case RecvType.Timeout:
                        {
                            Console.WriteLine("[TIMEOUT] Client timed out - ID: " + gameReceiveData.Connection.ID +
                                ", Address: " + gameReceiveData.Connection.IP + ":" + gameReceiveData.Connection.Port);
                            break;
                        }
                    case RecvType.Message:
                        {
                            Console.WriteLine("[MESSAGE] Packet received from - ID: " + gameReceiveData.Connection.ID +
                                ", Message: " + gameReceiveData.GameDataObject?.GetDescription());
                            break;
                        }
                }
            }
        }
    }
}
