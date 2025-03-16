using ENetServer.Management;
using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Net object containing DESERIALIZED network data JUST RECEIVED over the network. Must use Factory to create objects.
    /// </summary>
    public class GameRecvObject
    {
        public RecvType RecvType { get; }
        public Connection Connection { get; private set; }
        public GameDataObject? GameDataObject { get; }

        private GameRecvObject(RecvType recvType, Connection connection, GameDataObject? gameDataObject)
        {
            RecvType = recvType;
            Connection = connection;
            GameDataObject = gameDataObject;
        }



        /// <summary>
        /// Factory responsible for creating GameRecvObjects. Each method in this class corresponds to
        ///  one RecvType.
        /// </summary>
        internal static class Factory
        {
            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'connect' ENet event. Requires only
            ///  peer information, does not require a deserialized GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just connected. </param>
            /// <returns> The newly created 'connect' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromConnect(Connection connection)
            {
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
                return new GameRecvObject(RecvType.Message, connection, gameDataObject);
            }
        }
    }
}
