using ENet;
using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Net object containing SERIALIZED network data TO BE SENT over the network.
    ///  Use NetSendObject.Factory to create objects.
    /// </summary>
    internal class NetSendObject
    {
        internal SendType SendType { get; private set; }
        internal uint PeerID { get; private set; }
        internal byte[]? Bytes { get; private set; }

        private NetSendObject(SendType sendType, uint peerId, byte[]? bytes)
        {
            SendType = sendType;
            PeerID = peerId;
            Bytes = bytes;
        }

        private NetSendObject()
        {
            SendType = SendType.None;
            PeerID = 0;
            Bytes = null;
        }

        private NetSendObject Reconstruct(SendType sendType, uint peerId, byte[]? bytes)
        {
            SendType = sendType;
            PeerID = peerId;
            Bytes = bytes;
            return this;
        }

        private NetSendObject Reset()
        {
            SendType = SendType.None;
            PeerID = 0;
            Bytes = null;
            return this;
        }



        /// <summary>
        /// Factory responsible for creating NetSendObjects. Each creator method corresponds to
        ///  one SendType. Utilizes thread-safe static object pool.
        /// </summary>
        internal static class Factory
        {
            #region Object Pool and Methods

            private static readonly ConcurrentQueue<NetSendObject> pool = [];
            private static int targetPoolSize = 0;

            /// <summary>
            /// Sets the target object pool size. Completely empties and re-populates pool.
            /// </summary>
            /// <param name="poolSize"> Target number of elements to hold in the pool. </param>
            internal static void SetPoolSize(int poolSize)
            {
                // Completely empty pool, then fill pool with poolSize empty objects and set variable.
                pool.Clear();
                for (int i = 0; i < poolSize; i++)
                {
                    pool.Enqueue(new NetSendObject());
                }
                targetPoolSize = poolSize;
            }

            /// <summary>
            /// Returns a NetSendObject to the static object pool. Should always be called after
            ///  dequeuing and operating on a NetSendObject.
            /// </summary>
            /// <param name="netSendObject"> The NetSendObject to return to the pool. </param>
            internal static void ReturnToPool(NetSendObject netSendObject)
            {
                // Return to pool ONLY IF current pool count is <= target pool size.
                if (pool.Count <= targetPoolSize)
                {
                    pool.Enqueue(netSendObject.Reset());   // Reset object before returning to pool.
                }
            }

            #endregion



            /// <summary>
            /// Creates and returns a new NetSendObject formatted for disconnecting one client.
            ///  Requires only the ID of the peer to disconnect.
            /// </summary>
            /// <param name="peerId"> ID of peer to be disconnected. </param>
            /// <returns> The newly created 'disconnect one' NetSendObject. </returns>
            internal static NetSendObject CreateDisconnectOne(uint peerId)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netSendObject))
                //{
                //    return netSendObject.Reconstruct(SendType.Disconnect_One, peerId, null);
                //}

                // Else if no object is available, create new.
                return new NetSendObject(SendType.Disconnect_One, peerId, null);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject formatted for disconnecting all clients.
            ///  Requires no parameters because it is a universal operation.
            /// </summary>
            /// <returns> The newly created 'disconnect all' NetSendObject. </returns>
            internal static NetSendObject CreateDisconnectAll()
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netSendObject))
                //{
                //    return netSendObject.Reconstruct(SendType.Disconnect_All, 0, null);
                //}

                // Else if no object is available, create new.
                return new NetSendObject(SendType.Disconnect_All, 0, null);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject formatted for messaging one client. Requires
            ///  both a peer ID and a valid non-null byte[] of serialized GameDataObject data.
            /// </summary>
            /// <param name="peerId"> ID of peer to send message to. </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message one' NetSendObject. </returns>
            internal static NetSendObject CreateMessageOne(uint peerId, byte[] bytes)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netSendObject))
                //{
                //    return netSendObject.Reconstruct(SendType.Message_One, peerId, bytes);
                //}

                // Else if no object is available, create new.
                return new NetSendObject(SendType.Message_One, peerId, bytes);
            }


            /// <summary>
            /// Creates and returns a new NetSendObject formatted for messaging all clients.
            ///  Requires only a valid non-null byte[] of serialized GameDataObject data.
            /// </summary>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all' NetSendObject. </returns>
            internal static NetSendObject CreateMessageAll(byte[] bytes)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netSendObject))
                //{
                //    return netSendObject.Reconstruct(SendType.Message_All, 0, bytes);
                //}

                // Else if no object is available, create new.
                return new NetSendObject(SendType.Message_All, 0, bytes);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject formatted for messaging all clients except
            ///  one. Requires both a peer ID and a valid non-null byte[] of serialized GameDataObject data.
            /// </summary>
            /// <param name="peerId"> ID of peer to except sending this message to. </param>
            /// <param name="gameDataObject"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all except' NetSendObject. </returns>
            internal static NetSendObject CreateMessageAllExcept(uint peerId, byte[] bytes)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netSendObject))
                //{
                //    return netSendObject.Reconstruct(SendType.Message_AllExcept, peerId, bytes);
                //}

                // Else if no object is available, create new.
                return new NetSendObject(SendType.Message_AllExcept, peerId, bytes);
            }

            /// <summary>
            /// Creates and returns a new TEST NetSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="peerId"> TEST peer ID to simulate message send overhead. </param>
            /// <param name="bytes"> TEST byte[] to simulate message send overhead. </param>
            /// <returns> The newly created TEST NetSendObject. </returns>
            internal static NetSendObject CreateTestSend(uint peerId, byte[] bytes)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netSendObject))
                //{
                //    return netSendObject.Reconstruct(SendType.TestSend, peerId, bytes);
                //}

                // Else if no object is available, create new.
                return new NetSendObject(SendType.TestSend, peerId, bytes);
            }
        }
    }
}
