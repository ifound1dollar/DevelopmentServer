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
        private readonly ConcurrentQueue<MessageSendObject> gameToSerializeQueue;
        private readonly ConcurrentQueue<MessageSendObject> serializeToNetQueue;
        private readonly ConcurrentQueue<MessageRecvObject> netToSerializeQueue;
        private readonly ConcurrentQueue<MessageRecvObject> serializeToGameQueue;

        /// <summary>
        /// Constructs a Serializer object with references to message queues ((de)serializable data).
        /// </summary>
        /// <param name="gameToSerializeQueue"> Reference to outgoing game->serialize message queue. </param>
        /// <param name="serializeToNetQueue"> Reference to outgoing serialize->net message queue. </param>
        /// <param name="netToSerializeQueue"> Reference to incoming net->serialize message queue. </param>
        /// <param name="serializeToGameQueue"> Reference to incoming serialize->game message queue. </param>
        internal Serializer(ConcurrentQueue<MessageSendObject> gameToSerializeQueue,
            ConcurrentQueue<MessageSendObject> serializeToNetQueue,
            ConcurrentQueue<MessageRecvObject> netToSerializeQueue,
            ConcurrentQueue<MessageRecvObject> serializeToGameQueue)
        {
            this.gameToSerializeQueue = gameToSerializeQueue;
            this.serializeToNetQueue = serializeToNetQueue;
            this.netToSerializeQueue = netToSerializeQueue;
            this.serializeToGameQueue = serializeToGameQueue;
        }



        /// <summary>
        /// Handles game send tasks - read from game out queue, operate on data (serialize), add to network send queue.
        /// </summary>
        internal void DoSerializeTasks()
        {
            // Loop until outgoing game->serialize message queue is empty.
            while (!gameToSerializeQueue.IsEmpty)
            {
                //Console.WriteLine("Reading from GameToSerializeQueue");

                // Try to dequeue item from serializeQueue, operating on the item if successful.
                if (!gameToSerializeQueue.TryDequeue(out MessageSendObject? messageSendObject)) break;

                // Serialize GameDataObject data and return as byte[]. If null, log failure and return.
                byte[]? bytes = GameDataObject.SerializeGameDataObject(messageSendObject.GameDataObject);
                if (bytes == null)
                {
                    Console.WriteLine("[SERIALIZER_ERROR] Cannot serialize a null GameDataObject. Aborting.");
                    return;
                }

                // Set MessageSendObject byte[] with serialized data, nullify GameDataObject, and enqueue.
                messageSendObject.Bytes = bytes;
                messageSendObject.GameDataObject = null;
                serializeToNetQueue.Enqueue(messageSendObject);
            }
        }

        /// <summary>
        /// Handles game receive tasks - read from network receive queue, operate on data (deserialize), add to game in queue.
        /// </summary>
        internal void DoDeserializeTasks()
        {
            // Loop until incoming net->serialize message queue is empty.
            while (!netToSerializeQueue.IsEmpty)
            {
                //Console.WriteLine("Reading from NetToSerializeQueue");

                // Try to dequeue item from netToSerializeQueue, operating on the item if successful.
                if (!netToSerializeQueue.TryDequeue(out MessageRecvObject? messageRecvObject)) break;

                // Attempt to deserialize received byte[] into GameDataObject. Log error and return if failure.
                GameDataObject? gameDataObject = GameDataObject.DeserializeGameDataObject(messageRecvObject.Bytes);
                if (gameDataObject == null)
                {
                    Console.WriteLine("[SERIALIZER_ERROR] Failed to deserialize data received from peer ID {0}. Discarding.",
                        messageRecvObject.Connection.ID);
                    return;
                }

                // Set MessageSendObject GameDataObject with deserialized data, nullify byte[], and enqueue.
                messageRecvObject.GameDataObject = gameDataObject;
                messageRecvObject.Bytes = null;
                serializeToGameQueue.Enqueue(messageRecvObject);
            }
        }
    }
}
