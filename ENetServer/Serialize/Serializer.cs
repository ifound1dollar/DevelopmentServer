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
                    case SendType.Disconnect_One:
                    case SendType.Disconnect_Many:
                    case SendType.Disconnect_All:
                        {
                            // Simply pass incoming non-message object to network thread to be handled there.
                            NetSendObject netSendObject = new(gameSendObject.SendType, gameSendObject.PeerParams);
                            netSendQueue.Enqueue(netSendObject);

                            break;
                        }
                    case SendType.Message_One:
                    case SendType.Message_Many:
                    case SendType.Message_All:
                    case SendType.Message_AllExcept:
                    case SendType.TestSend:
                        {
                            // Verify GameDataObject is not null before trying to serialize.
                            if (gameSendObject.GameDataObject == null)
                            {
                                // Else unsuccessful, so log error and do not enqueue.
                                Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                                return;
                            }

                            // Serialize GameDataObject into byte[], then enqueue to net.
                            int length = GameDataObject.SerializeGameDataObject(gameSendObject.GameDataObject,
                                out byte[] bytes);
                            NetSendObject netSendObject = new(gameSendObject.SendType, gameSendObject.PeerParams,
                                bytes, length);
                            netSendQueue.Enqueue(netSendObject);
                            
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
                if (!netRecvQueue.TryDequeue(out NetRecvObject? netRecvObject)) break;

                // Operate based on receive type.
                switch (netRecvObject.RecvType)
                {
                    case RecvType.Connect:
                    case RecvType.Disconnect:
                    case RecvType.Timeout:
                        {
                            // Simply pass incoming non-message object to main/game thread to be handled there.
                            GameRecvObject gameRecvObject = new(netRecvObject.RecvType, netRecvObject.Connection);
                            gameRecvQueue.Enqueue(gameRecvObject);

                            break;
                        }
                    case RecvType.Message:
                    case RecvType.TestRecv:
                        {
                            // Verify byte[] is not null before attempting to deserialize.
                            if (netRecvObject.Bytes == null) return;

                            // Attempt to deserialize received byte[] into GameDataObject.
                            if (GameDataObject.DeserializeGameDataObject(netRecvObject.Bytes, netRecvObject.Length,
                                out GameDataObject? gameDataObject))
                            {
                                // If deserialization successful, create GameRecvObject and enqueue to game.
                                GameRecvObject gameRecvObject = new(netRecvObject.RecvType, netRecvObject.Connection, gameDataObject);
                                gameRecvQueue.Enqueue(gameRecvObject);
                            }
                            else
                            {
                                // Else unsuccessful, so log error and do not enqueue.
                                Console.WriteLine("[SERIALIZER_ERROR] Failed to deserialize data received from peer ID {0}. Discarding.",
                                    netRecvObject.Connection.ID);
                            }

                            break;
                        }
                    // DO NOTHING FOR DEFAULT CASE
                }

                /* - outside of switch case, inside while loop - */
            }
        }

    }
}
