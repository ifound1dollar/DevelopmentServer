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
    /// Net object containing NON-SERIALIZED network data TO BE SENT over the network. Use GameSendObject.Factory to create objects.
    /// </summary>
    public class GameSendObject
    {
        public SendType SendType { get; }
        public uint PeerID { get; }
        public GameDataObject? GameDataObject { get; }

        private GameSendObject(SendType sendType, uint peerId, GameDataObject? gameDataObject)
        {
            SendType = sendType;
            PeerID = peerId;
            GameDataObject = gameDataObject;
        }



        /// <summary>
        /// Factory responsible for creating GameSendObjects. Each method in this class corresponds
        ///  to one SendType.
        /// </summary>
        public static class Factory
        {
            private static ConcurrentBag<GameSendObject> pool = [];

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for disconnecting one client.
            ///  Requires only the ID of the peer to disconnect.
            /// </summary>
            /// <param name="peerId"> ID of peer to be disconnected. </param>
            /// <returns> The newly created 'disconnect one' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectOne(uint peerId)
            {
                return new GameSendObject(SendType.Disconnect_One, peerId, null);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for disconnecting all clients.
            ///  Requires no parameters because it is a universal operation.
            /// </summary>
            /// <returns> The newly created 'disconnect all' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectAll()
            {
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
                return new GameSendObject(SendType.TestSend, peerId, gameDataObject);
            }

        }
    }
}
