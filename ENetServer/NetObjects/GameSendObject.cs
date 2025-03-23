using ENet;
using ENetServer.Network;
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
        public SendType SendType { get; }
        public Connection Connection { get; }
        public GameDataObject? GameDataObject { get; }

        private GameSendObject(SendType sendType, Connection connection, GameDataObject? gameDataObject)
        {
            SendType = sendType;
            Connection = connection;
            GameDataObject = gameDataObject;
        }



        /// <summary>
        /// Factory responsible for creating GameSendObjects. Each creator method corresponds to
        ///  one SendType.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Creates and returns a new GameSendObject formatted for connecting to one remote
            ///  host. Requires an IP address and a port number.
            /// </summary>
            /// <param name="connection"> Connection containing data for peer to connect to. </param>
            /// <returns></returns>
            public static GameSendObject CreateConnectOne(Connection connection)
            {
                return new GameSendObject(SendType.Connect_One, connection, null);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for disconnecting one remote
            ///  host. Requires only a valid Connection object.
            /// </summary>
            /// <param name="connection"> Connection for remote host to disconnect. </param>
            /// <returns> The newly created 'disconnect one' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectOne(Connection connection)
            {
                return new GameSendObject(SendType.Disconnect_One, connection, null);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for disconnecting all remote
            ///  hosts. Requires no parameters because it is a universal operation.
            /// </summary>
            /// <param name="connection"> Connection containing only whether to disconnect server or client peers. </param>
            /// <returns> The newly created 'disconnect all' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectAll(Connection connection)
            {
                return new GameSendObject(SendType.Disconnect_All, connection, null);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for messaging one remote host.
            ///  Requires a valid non-null Connection and GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection for remote host to message. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message one' GameSendObject. </returns>
            public static GameSendObject CreateMessageOne(Connection connection, GameDataObject gameDataObject)
            {
                return new GameSendObject(SendType.Message_One, connection, gameDataObject);
            }


            /// <summary>
            /// Creates and returns a new GameSendObject formatted for messaging all remote
            ///  hosts. Requires only a valid non-null GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection containing only whether to disconnect server or client peers. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all' GameSendObject. </returns>
            public static GameSendObject CreateMessageAll(Connection connection, GameDataObject gameDataObject)
            {
                return new GameSendObject(SendType.Message_All, connection, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject formatted for message all remote hosts
            ///  except one. Requires a valid non-null Connection and GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection for remote host to ignore. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all except' GameSendObject. </returns>
            public static GameSendObject CreateMessageAllExcept(Connection connection, GameDataObject gameDataObject)
            {
                return new GameSendObject(SendType.Message_AllExcept, connection, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new TEST GameSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="connection"> TEST Connection object to simulate message send overhead. </param>
            /// <param name="gameDataObject"> TEST GameDataObject to simulate message send overhead. </param>
            /// <returns> The newly created TEST GameSendObject. </returns>
            public static GameSendObject CreateTestSend(Connection connection, GameDataObject gameDataObject)
            {
                return new GameSendObject(SendType.TestSend, connection, gameDataObject);
            }

        }
    }
}
