using ENet;
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
    /// Net object containing SERIALIZED network data TO BE SENT over the network. Must use Factory to create objects.
    /// </summary>
    internal class NetworkSendObject
    {
        internal SendType SendType { get; }
        internal uint PeerID { get; }
        internal byte[]? Bytes { get; }

        private NetworkSendObject(SendType sendType, uint peerId, byte[]? bytes)
        {
            SendType = sendType;
            PeerID = peerId;
            Bytes = bytes;
        }



        /// <summary>
        /// Factory responsible for creating NetworkSendObjects. Each method in this class corresponds
        ///  to one SendType.
        /// </summary>
        internal static class Factory
        {
            /// <summary>
            /// Creates and returns a new NetworkSendObject formatted for disconnecting one client.
            ///  Requires only the ID of the peer to disconnect.
            /// </summary>
            /// <param name="peerId"> ID of peer to be disconnected. </param>
            /// <returns> The newly created 'disconnect one' NetworkSendObject. </returns>
            internal static NetworkSendObject CreateDisconnectOne(uint peerId)
            {
                return new NetworkSendObject(SendType.Disconnect_One, peerId, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkSendObject formatted for disconnecting all clients.
            ///  Requires no parameters because it is a universal operation.
            /// </summary>
            /// <returns> The newly created 'disconnect all' NetworkSendObject. </returns>
            internal static NetworkSendObject CreateDisconnectAll()
            {
                return new NetworkSendObject(SendType.Disconnect_All, 0, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkSendObject formatted for messaging one client. Requires
            ///  both a peer ID and a valid non-null byte[] of serialized GameDataObject data.
            /// </summary>
            /// <param name="peerId"> ID of peer to send message to. </param>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message one' NetworkSendObject. </returns>
            internal static NetworkSendObject CreateMessageOne(uint peerId, byte[] bytes)
            {
                return new NetworkSendObject(SendType.Message_One, peerId, bytes);
            }


            /// <summary>
            /// Creates and returns a new NetworkSendObject formatted for messaging all clients.
            ///  Requires only a valid non-null byte[] of serialized GameDataObject data.
            /// </summary>
            /// <param name="bytes"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all' NetworkSendObject. </returns>
            internal static NetworkSendObject CreateMessageAll(byte[] bytes)
            {
                return new NetworkSendObject(SendType.Message_All, 0, bytes);
            }

            /// <summary>
            /// Creates and returns a new NetworkSendObject formatted for messaging all clients except
            ///  one. Requires both a peer ID and a valid non-null byte[] of serialized GameDataObject data.
            /// </summary>
            /// <param name="peerId"> ID of peer to except sending this message to. </param>
            /// <param name="gameDataObject"> byte[] of serialized GameDataObject data. Must not be null. </param>
            /// <returns> The newly created 'message all except' NetworkSendObject. </returns>
            internal static NetworkSendObject CreateMessageAllExcept(uint peerId, byte[] bytes)
            {
                return new NetworkSendObject(SendType.Message_AllExcept, peerId, bytes);
            }
        }
    }
}
