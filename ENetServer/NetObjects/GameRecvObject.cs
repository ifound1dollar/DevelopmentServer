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
    /// Net object containing DESERIALIZED network data JUST RECEIVED over the network.
    ///  Use GameRecvObject.Factory to create objects.
    /// </summary>
    public class GameRecvObject
    {
        // Empty Connection object used for un-constructed object.
        private static readonly Connection emptyConnection = new(0, "0.0.0.0", 0);

        public RecvType RecvType { get; private set; }
        public Connection Connection { get; private set; }
        public GameDataObject? GameDataObject { get; private set; }

        private GameRecvObject(RecvType recvType, Connection connection, GameDataObject? gameDataObject)
        {
            RecvType = recvType;
            Connection = connection;
            GameDataObject = gameDataObject;
        }

        private GameRecvObject()
        {
            RecvType = RecvType.None;
            Connection = emptyConnection;
            GameDataObject = null;
        }

        private GameRecvObject Reconstruct(RecvType recvType, Connection connection, GameDataObject? gameDataObject)
        {
            RecvType = recvType;
            Connection = connection;
            GameDataObject = gameDataObject;
            return this;
        }

        private GameRecvObject Reset()
        {
            RecvType = RecvType.None;
            Connection = emptyConnection;
            GameDataObject = null;
            return this;
        }



        /// <summary>
        /// Factory responsible for creating GameRecvObjects. Each creator method corresponds to
        ///  one RecvType. Utilizes thread-safe static object pool.
        /// </summary>
        internal static class Factory
        {
            #region Object Pool and Methods

            private static readonly ConcurrentQueue<GameRecvObject> pool = [];
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
                    pool.Enqueue(new GameRecvObject());
                }
                targetPoolSize = poolSize;
            }

            /// <summary>
            /// Returns a GameRecvObject to the static object pool. Should always be called after
            ///  dequeuing and operating on a GameRecvObject.
            /// </summary>
            /// <param name="gameRecvObject"> The GameRecvObject to return to the pool. </param>
            internal static void ReturnToPool(GameRecvObject gameRecvObject)
            {
                // Return to pool ONLY IF current pool count is <= target pool size.
                if (pool.Count <= targetPoolSize)
                {
                    pool.Enqueue(gameRecvObject.Reset());   // Reset object before returning to pool.
                }
            }

            #endregion



            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'connect' ENet event. Requires only
            ///  peer information, does not require a deserialized GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just connected. </param>
            /// <returns> The newly created 'connect' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromConnect(Connection connection)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameRecvObject))
                //{
                //    return gameRecvObject.Reconstruct(RecvType.Connect, connection, null);
                //}

                // Else if no object is available, create new.
                return new GameRecvObject(RecvType.Connect, connection, null);
            }

            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'disconnect' ENet event. Requires only
            ///  peer information, does not require a deserialized GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just disconnected. </param>
            /// <returns> The newly created 'disconnect' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromDisconnect(Connection connection)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameRecvObject))
                //{
                //    return gameRecvObject.Reconstruct(RecvType.Disconnect, connection, null);
                //}

                // Else if no object is available, create new.
                return new GameRecvObject(RecvType.Disconnect, connection, null);
            }

            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'timeout' ENet event. Requires only
            ///  peer information, does not require a deserialized GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just timed out. </param>
            /// <returns> The newly created 'timeout' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromTimeout(Connection connection)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameRecvObject))
                //{
                //    return gameRecvObject.Reconstruct(RecvType.Timeout, connection, null);
                //}

                // Else if no object is available, create new.
                return new GameRecvObject(RecvType.Timeout, connection, null);
            }

            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'message' ENet event. Requires
            ///  peer information and a valid non-null deserialized GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that message was received from. </param>
            /// <param name="gameDataObject"> GameDataObject deserialized from the received byte[] payload. Must not be null. </param>
            /// <returns> The newly created 'message' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromMessage(Connection connection, GameDataObject gameDataObject)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameRecvObject))
                //{
                //    return gameRecvObject.Reconstruct(RecvType.Message, connection, gameDataObject);
                //}

                // Else if no object is available, create new.
                return new GameRecvObject(RecvType.Message, connection, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new TEST GameRecvObject, which was not actually received over the
            ///  but was simply re-queued by the network thread.
            /// </summary>
            /// <param name="connection"> TEST Connection to simulate message receive overhead. </param>
            /// <param name="gameDataObject"> TEST GameDataObject to simulate message receive overhead. </param>
            /// <returns> The newly created TEST GameRecvObject. </returns>
            internal static GameRecvObject CreateFromTestRecv(Connection connection, GameDataObject gameDataObject)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameRecvObject))
                //{
                //    return gameRecvObject.Reconstruct(RecvType.TestRecv, connection, gameDataObject);
                //}

                // Else if no object is available, create new.
                return new GameRecvObject(RecvType.TestRecv, connection, gameDataObject);
            }
        }
    }
}
