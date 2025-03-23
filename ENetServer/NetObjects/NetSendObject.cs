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
    /// Net object containing SERIALIZED network data TO BE SENT over the network.
    ///  Use NetSendObject.Factory to create objects.
    /// </summary>
    internal class NetSendObject
    {
        internal SendType SendType { get; }
        internal Connection Connection { get; }
        internal byte[]? Bytes { get; }

        private NetSendObject(SendType sendType, Connection connection, byte[]? bytes)
        {
            SendType = sendType;
            Connection = connection;
            Bytes = bytes;
        }



        /// <summary>
        /// Factory responsible for creating NetSendObjects. Each creator method corresponds to
        ///  one SendType.
        /// </summary>
        internal static class Factory
        {
            /// <summary>
            /// Creates and returns a new NetSendObject formatted for connecting to one remote
            ///  host. Requires an IP address and a port number.
            /// </summary>
            /// <param name="connection"> Connection for remote host to connect to (only contains IP and port). </param>
            /// <returns></returns>
            public static NetSendObject CreateConnectOne(Connection connection)
            {
                return new NetSendObject(SendType.Connect_One, connection, null);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject formatted for disconnecting one remote
            ///  host. Requires only a valid Connection object.
            /// </summary>
            /// <param name="connection"> Connection for remote host to disconnect. </param>
            /// <returns> The newly created 'disconnect one' NetSendObject. </returns>
            public static NetSendObject CreateDisconnectOne(Connection connection)
            {
                return new NetSendObject(SendType.Disconnect_One, connection, null);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject formatted for disconnecting all remote
            ///  hosts. Requires no parameters because it is a universal operation.
            /// </summary>
            /// <param name="connection"> Connection containing whether to send to client or server peers. </param>
            /// <returns> The newly created 'disconnect all' NetSendObject. </returns>
            public static NetSendObject CreateDisconnectAll(Connection connection)
            {
                return new NetSendObject(SendType.Disconnect_All, connection, null);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject formatted for messaging one remote host.
            ///  Requires a valid non-null Connection and GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection for remote host to message. </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message one' NetSendObject. </returns>
            public static NetSendObject CreateMessageOne(Connection connection, byte[] bytes)
            {
                return new NetSendObject(SendType.Message_One, connection, bytes);
            }


            /// <summary>
            /// Creates and returns a new NetSendObject formatted for messaging all remote
            ///  hosts. Requires only a valid non-null GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection containing whether to send to client or server peers. </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all' NetSendObject. </returns>
            public static NetSendObject CreateMessageAll(Connection connection, byte[] bytes)
            {
                return new NetSendObject(SendType.Message_All, connection, bytes);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject formatted for message all remote hosts
            ///  except one. Requires a valid non-null Connection and GameDataObject.
            /// </summary>
            /// <param name="connection"> Connection for remote host to ignore. </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all except' NetSendObject. </returns>
            public static NetSendObject CreateMessageAllExcept(Connection connection, byte[] bytes)
            {
                return new NetSendObject(SendType.Message_AllExcept, connection, bytes);
            }

            /// <summary>
            /// Creates and returns a new TEST NetSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="connection"> TEST Connection object to simulate message send overhead. </param>
            /// <param name="bytes"> TEST byte[] to simulate message send overhead. </param>
            /// <returns> The newly created TEST NetSendObject. </returns>
            public static NetSendObject CreateTestSend(Connection connection, byte[] bytes)
            {
                return new NetSendObject(SendType.TestSend, connection, bytes);
            }

        }
    }
}
