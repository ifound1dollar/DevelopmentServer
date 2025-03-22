using ENetServer.Management;
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
    /// Net object containing SERIALIZED network data JUST RECEIVED over the network.
    ///  Use NetRecvObject.Factory to create objects.
    /// </summary>
    internal class NetRecvObject
    {
        // Empty Connection object used for un-constructed object.
        private static readonly Connection emptyConnection = new(0, "0.0.0.0", 0);

        internal RecvType RecvType { get; private set; }
        internal Connection Connection { get; private set; }
        internal byte[]? Bytes { get; private set; }

        private NetRecvObject(RecvType recvType, Connection connection, byte[]? bytes)
        {
            RecvType = recvType;
            Connection = connection;
            Bytes = bytes;
        }

        private NetRecvObject()
        {
            RecvType = RecvType.None;
            Connection = emptyConnection;
            Bytes = null;
        }

        private NetRecvObject Reconstruct(RecvType recvType, Connection connection, byte[]? bytes)
        {
            RecvType = recvType;
            Connection = connection;
            Bytes = bytes;
            return this;
        }

        private NetRecvObject Reset()
        {
            RecvType = RecvType.None;
            Connection = emptyConnection;
            Bytes = null;
            return this;
        }



        /// <summary>
        /// Factory responsible for creating NetworkRecvObjects. Each creator method corresponds to
        ///  one RecvType. Utilizes thread-safe static object pool.
        /// </summary>
        internal static class Factory
        {
            #region Object Pool and Methods

            private static readonly ConcurrentQueue<NetRecvObject> pool = [];
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
                    pool.Enqueue(new NetRecvObject());
                }
                targetPoolSize = poolSize;
            }

            /// <summary>
            /// Returns a NetRecvObject to the static object pool. Should always be called after
            ///  dequeuing and operating on a NetRecvObject.
            /// </summary>
            /// <param name="netRecvObject"> The NetRecvObject to return to the pool. </param>
            internal static void ReturnToPool(NetRecvObject netRecvObject)
            {
                // Return to pool ONLY IF current pool count is <= target pool size.
                if (pool.Count <= targetPoolSize)
                {
                    pool.Enqueue(netRecvObject.Reset());   // Reset object before returning to pool.
                }
            }

            #endregion



            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'connect' ENet event. Requires only
            ///  peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just connected. </param>
            /// <returns> The newly created 'connect' NetworkRecvObject. </returns>
            internal static NetRecvObject CreateFromConnect(Connection connection)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netRecvObject))
                //{
                //    return netRecvObject.Reconstruct(RecvType.Connect, connection, null);
                //}

                // Else if no object is available, create new.
                return new NetRecvObject(RecvType.Connect, connection, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'disconnect' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just disconnected. </param>
            /// <returns> The newly created 'disconnect' NetworkRecvObject. </returns>
            internal static NetRecvObject CreateFromDisconnect(Connection connection)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netRecvObject))
                //{
                //    return netRecvObject.Reconstruct(RecvType.Disconnect, connection, null);
                //}

                // Else if no object is available, create new.
                return new NetRecvObject(RecvType.Disconnect, connection, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'timeout' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just timed out. </param>
            /// <returns> The newly created 'timeout' NetworkRecvObject. </returns>
            internal static NetRecvObject CreateFromTimeout(Connection connection)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netRecvObject))
                //{
                //    return netRecvObject.Reconstruct(RecvType.Timeout, connection, null);
                //}

                // Else if no object is available, create new.
                return new NetRecvObject(RecvType.Timeout, connection, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'message' ENet event. Requires
            ///  peer information and byte[] payload of incoming message packet.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that message was received from. </param>
            /// <param name="bytes"> The incoming message packet payload as byte[]. </param>
            /// <returns> The newly created 'message' NetworkRecvObject. </returns>
            internal static NetRecvObject CreateFromMessage(Connection connection, byte[] bytes)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netRecvObject))
                //{
                //    return netRecvObject.Reconstruct(RecvType.Message, connection, bytes);
                //}

                // Else if no object is available, create new.
                return new NetRecvObject(RecvType.Message, connection, bytes);
            }

            /// <summary>
            /// Creates and returns a new TEST NetRecvObject, which was not actually received over the
            ///  but was simply re-queued by the network thread.
            /// </summary>
            /// <param name="connection"> TEST Connection to simulate message receive overhead. </param>
            /// <param name="bytes"> TEST byte[] to simulate message receive overhead. </param>
            /// <returns> The newly created TEST GameRecvObject. </returns>
            internal static NetRecvObject CreateFromTestRecv(Connection connection, byte[] bytes)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var netRecvObject))
                //{
                //    return netRecvObject.Reconstruct(RecvType.TestRecv, connection, bytes);
                //}

                // Else if no object is available, create new.
                return new NetRecvObject(RecvType.TestRecv, connection, bytes);
            }
        }
    }
}
