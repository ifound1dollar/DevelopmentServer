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
        private readonly ConcurrentQueue<NetSendObject> gameSendQueue;
        private readonly ConcurrentQueue<NetSendObject> netSendQueue;
        private readonly ConcurrentQueue<NetRecvObject> gameRecvQueue;
        private readonly ConcurrentQueue<NetRecvObject> netRecvQueue;

        /// <summary>
        /// Constructs a Serializer object with references to game AND network concurrent queues.
        /// </summary>
        /// <param name="gameSendQueue"> Reference to game send queue. </param>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="gameRecvQueue"> Reference to game receive queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Serializer(ConcurrentQueue<NetSendObject> gameSendQueue,
            ConcurrentQueue<NetSendObject> netSendQueue,
            ConcurrentQueue<NetRecvObject> gameRecvQueue,
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
                if (!gameSendQueue.TryDequeue(out NetSendObject? netSendObject)) break;

                switch (netSendObject.SendType)
                {
                    case SendType.Connect_One:
                    case SendType.Disconnect_One:
                    case SendType.Disconnect_Many:
                    case SendType.Disconnect_All:
                        {
                            netSendQueue.Enqueue(netSendObject);
                            break;
                        }
                    case SendType.Message_One:
                    case SendType.Message_Many:
                    case SendType.Message_All:
                    case SendType.Message_AllExcept:
                    case SendType.TestSend:
                        {
                            // Attempt to serialize GameDataObject into byte[], logging error and skipping if failure.
                            byte[]? bytes = GameDataObject.SerializeGameDataObject(netSendObject.GameDataObject);
                            if (bytes == null)
                            {
                                Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                                continue;
                            }

                            // If serialization was successful, set byte[] and nullify GameDataObject.
                            netSendObject.Bytes = bytes;
                            netSendObject.GameDataObject = null;
                            netSendQueue.Enqueue(netSendObject);
                            break;
                        }
                }

                //// If NetSendObject is not serializable (not message), simply re-enqueue to network thread.
                //if (!netSendObject.IsSerializable)
                //{
                //    netSendQueue.Enqueue(netSendObject);
                //}
                //// Else serialize NetSendObject and then enqueue to network thread.
                //else
                //{
                //    // Attempt to serialize GameDataObject into byte[], logging error and skipping if failure.
                //    byte[]? bytes = GameDataObject.SerializeGameDataObject(netSendObject.GameDataObject);
                //    if (bytes == null)
                //    {
                //        Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                //        continue;
                //    }

                //    // If serialization was successful, set byte[] and nullify GameDataObject.
                //    netSendObject.Bytes = bytes;
                //    //netSendObject.GameDataObject = null;
                //    netSendQueue.Enqueue(netSendObject);
                //}
                
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

                switch (netRecvObject.RecvType)
                {
                    case RecvType.Connect:
                    case RecvType.Disconnect:
                    case RecvType.Timeout:
                        {
                            gameRecvQueue.Enqueue(netRecvObject);
                            break;
                        }
                    case RecvType.Message:
                    case RecvType.TestRecv:
                        {
                            // Attempt to desserialize byte[] into GameDataObject, logging error and skipping if failure.
                            GameDataObject? gameDataObject = GameDataObject.DeserializeGameDataObject(netRecvObject.Bytes);
                            if (gameDataObject == null)
                            {
                                Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                                continue;
                            }

                            // If serialization was successful, set GameDataObject and nullify byte[].
                            netRecvObject.GameDataObject = gameDataObject;
                            netRecvObject.Bytes = null;
                            gameRecvQueue.Enqueue(netRecvObject);

                            break;
                        }
                }

                // If NetRecvObject is not deserializable (not message), simply re-enqueue to game thread.
                //if (!netRecvObject.IsDeserializable)
                //{
                //    gameRecvQueue.Enqueue(netRecvObject);
                //}
                //// Else deserialize NetRecvObject and then enqueue to game thread.
                //else
                //{
                //    // Attempt to desserialize byte[] into GameDataObject, logging error and skipping if failure.
                //    GameDataObject? gameDataObject = GameDataObject.DeserializeGameDataObject(netRecvObject.Bytes);
                //    if (gameDataObject == null)
                //    {
                //        Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                //        continue;
                //    }

                //    // If serialization was successful, set GameDataObject and nullify byte[].
                //    netRecvObject.GameDataObject = gameDataObject;
                //    //netRecvObject.Bytes = null;
                //    gameRecvQueue.Enqueue(netRecvObject);
                //}

            }
        }

    }
}
