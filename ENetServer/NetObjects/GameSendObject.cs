using ENet;
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
    /// Net object containing NON-SERIALIZED network data TO BE SENT over the network.
    ///  Use GameSendObject.Factory to create objects.
    /// </summary>
    public class GameSendObject
    {
        public SendType SendType { get; private set; }
        public uint PeerID { get; private set; }
        public GameDataObject? GameDataObject { get; private set; }

        private GameSendObject(SendType sendType, uint peerId, GameDataObject? gameDataObject)
        {
            SendType = sendType;
            PeerID = peerId;
            GameDataObject = gameDataObject;
        }

        private GameSendObject()
        {
            SendType = SendType.None;
            PeerID = 0;
            GameDataObject = null;
        }

        private GameSendObject Reconstruct(SendType sendType, uint peerId, GameDataObject? gameDataObject)
        {
            SendType = sendType;
            PeerID = peerId;
            GameDataObject = gameDataObject;
            return this;
        }

        private GameSendObject Reset()
        {
            SendType = SendType.None;
            PeerID = 0;
            GameDataObject = null;
            return this;
        }



        /// <summary>
        /// Factory responsible for creating GameSendObjects. Each creator method corresponds to
        ///  one SendType. Utilizes thread-safe static object pool.
        /// </summary>
        public static class Factory
        {
            #region Object Pool and Methods

            private static readonly ConcurrentQueue<GameSendObject> pool = [];
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
                    pool.Enqueue(new GameSendObject());
                }
                targetPoolSize = poolSize;
            }

            /// <summary>
            /// Returns a GameSendObject to the static object pool. Should always be called after
            ///  dequeuing and operating on a GameSendObject.
            /// </summary>
            /// <param name="gameSendObject"> The GameSendObject to return to the pool. </param>
            internal static void ReturnToPool(GameSendObject gameSendObject)
            {
                // Return to pool ONLY IF current pool count is <= target pool size.
                if (pool.Count <= targetPoolSize)
                {
                    pool.Enqueue(gameSendObject.Reset());   // Reset object before returning to pool.
                }
            }

            #endregion



            /// <summary>
            /// Creates and returns a new GameSendObject formatted for disconnecting one client.
            ///  Requires only the ID of the peer to disconnect.
            /// </summary>
            /// <param name="peerId"> ID of peer to be disconnected. </param>
            /// <returns> The newly created 'disconnect one' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectOne(uint peerId)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameSendObject))
                //{
                //    return gameSendObject.Reconstruct(SendType.Disconnect_One, peerId, null);
                //}

                // Else if no object is available, create new.
                return new GameSendObject(SendType.Disconnect_One, peerId, null);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for disconnecting all clients.
            ///  Requires no parameters because it is a universal operation.
            /// </summary>
            /// <returns> The newly created 'disconnect all' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectAll()
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameSendObject))
                //{
                //    return gameSendObject.Reconstruct(SendType.Disconnect_All, 0, null);
                //}

                // Else if no object is available, create new.
                return new GameSendObject(SendType.Disconnect_All, 0, null);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for messaging one client. Requires
            ///  both the ID of the peer to message and a valid non-null GameDataObject.
            /// </summary>
            /// <param name="peerId"> ID of peer to send message to. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message one' GameSendObject. </returns>
            public static GameSendObject CreateMessageOne(uint peerId, GameDataObject gameDataObject)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameSendObject))
                //{
                //    return gameSendObject.Reconstruct(SendType.Message_One, peerId, gameDataObject);
                //}

                // Else if no object is available, create new.
                return new GameSendObject(SendType.Message_One, peerId, gameDataObject);
            }


            /// <summary>
            /// Creates and returns a new GameSendObject formatted for messaging all clients.
            ///  Requires only a valid non-null GameDataObject.
            /// </summary>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all' GameSendObject. </returns>
            public static GameSendObject CreateMessageAll(GameDataObject gameDataObject)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameSendObject))
                //{
                //    return gameSendObject.Reconstruct(SendType.Message_All, 0, gameDataObject);
                //}

                // Else if no object is available, create new.
                return new GameSendObject(SendType.Message_All, 0, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for message all clients except
            ///  one. Requires both the ID of the peer NOT to message and a valid non-null GameDataObject.
            /// </summary>
            /// <param name="peerId"> ID of peer to except sending this message to. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all except' GameSendObject. </returns>
            public static GameSendObject CreateMessageAllExcept(uint peerId, GameDataObject gameDataObject)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameSendObject))
                //{
                //    return gameSendObject.Reconstruct(SendType.Message_AllExcept, peerId, gameDataObject);
                //}

                // Else if no object is available, create new.
                return new GameSendObject(SendType.Message_AllExcept, peerId, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new TEST GameSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="peerId"> TEST peer ID to simulate message send overhead. </param>
            /// <param name="gameDataObject"> TEST GameDataObject to simulate message send overhead. </param>
            /// <returns> The newly created TEST GameSendObject. </returns>
            public static GameSendObject CreateTestSend(uint peerId, GameDataObject gameDataObject)
            {
                // If successfully pulls from pool, re-initialize members and return the object.
                //if (pool.TryDequeue(out var gameSendObject))
                //{
                //    return gameSendObject.Reconstruct(SendType.TestSend, peerId, gameDataObject);
                //}

                // Else if no object is available, create new.
                return new GameSendObject(SendType.TestSend, peerId, gameDataObject);
            }

        }
    }
}
