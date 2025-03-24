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
        internal PeerParams PeerParams { get; }
        internal byte[]? Bytes { get; }

        private NetSendObject(SendType sendType, PeerParams peerParams)
        {
            SendType = sendType;
            PeerParams = peerParams;
            // Bytes remains null.
        }

        private NetSendObject(SendType sendType, PeerParams peerParams, byte[]? bytes)
        {
            SendType = sendType;
            PeerParams = peerParams;
            Bytes = bytes;
        }



        /// <summary>
        /// Factory responsible for creating NetSendObjects. Each creator method corresponds to
        ///  one SendType.
        /// </summary>
        internal static class Factory
        {
            // NOTE: Each of the below Factory methods are almost identical (generally violating
            //  the DRY principle) and could reasonably be consolidated into two methods: those
            //  with payload data, and those without.
            // However, using separate Factory methods for each SendType enforces safe creation
            //  of these objects and gurantees that they will be formatted correctly (ex. an
            //  object to be used for Connect cannot accidentally have the wrong SendType that
            //  does not match the passed-in PeerParams object).

            /// <summary>
            /// Creates and returns a new NetSendObject for connecting to one remote host.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (IP and Port). </param>
            /// <returns> The newly created 'connect one' NetSendObject. </returns>
            internal static NetSendObject CreateConnectOne(PeerParams peerParams)
            {
                return new NetSendObject(SendType.Connect_One, peerParams);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject for disconnecting [from] one remote host.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (ID only). </param>
            /// <returns> The newly created 'disconnect one' NetSendObject. </returns>
            internal static NetSendObject CreateDisconnectOne(PeerParams peerParams)
            {
                return new NetSendObject(SendType.Disconnect_One, peerParams);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject for disconnecting [from] many remote hosts.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (array of IDs only). </param>
            /// <returns> The newly created 'disconnect many' NetSendObject. </returns>
            internal static NetSendObject CreateDisconnectMany(PeerParams peerParams)
            {
                return new NetSendObject(SendType.Disconnect_Many, peerParams);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject for disconnecting all remote hosts.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (HostType only). </param>
            /// <returns> The newly created 'disconnect all' NetSendObject. </returns>
            internal static NetSendObject CreateDisconnectAll(PeerParams peerParams)
            {
                return new NetSendObject(SendType.Disconnect_All, peerParams);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject for messaging one remote host.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (ID only). </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message one' NetSendObject. </returns>
            internal static NetSendObject CreateMessageOne(PeerParams peerParams, byte[] bytes)
            {
                return new NetSendObject(SendType.Message_One, peerParams, bytes);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject for messaging many remote hosts.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (array of IDs only). </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message many' NetSendObject. </returns>
            internal static NetSendObject CreateMessageMany(PeerParams peerParams, byte[] bytes)
            {
                return new NetSendObject(SendType.Message_Many, peerParams, bytes);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject for messaging all remote hosts.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (HostType only). </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all' NetSendObject. </returns>
            internal static NetSendObject CreateMessageAll(PeerParams peerParams, byte[] bytes)
            {
                return new NetSendObject(SendType.Message_All, peerParams, bytes);
            }

            /// <summary>
            /// Creates and returns a new NetSendObject for message all remote hosts except one.
            /// </summary>
            /// <param name="peerParams"> PeerParams containing necessary peer data (HostType and ID). </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all except' NetSendObject. </returns>
            internal static NetSendObject CreateMessageAllExcept(PeerParams peerParams, byte[] bytes)
            {
                return new NetSendObject(SendType.Message_AllExcept, peerParams, bytes);
            }



            /// <summary>
            /// Creates and returns a new TEST NetSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="peerParams"> TEST PeerParams object to simulate message send overhead. </param>
            /// <param name="bytes"> TEST byte[] to simulate message send overhead. </param>
            /// <returns> The newly created TEST NetSendObject. </returns>
            internal static NetSendObject CreateTestSend(PeerParams peerParams, byte[] bytes)
            {
                return new NetSendObject(SendType.TestSend, peerParams, bytes);
            }

        }
    }
}
