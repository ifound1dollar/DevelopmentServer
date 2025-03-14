﻿using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Net object containing SERIALIZED network data JUST RECEIVED over the network. Must use Factory to create objects.
    /// </summary>
    internal class NetworkRecvObject
    {
        internal RecvType RecvType { get; }
        internal uint PeerID { get; }
        internal string PeerIP { get; }
        internal ushort PeerPort { get; }
        internal byte[]? Bytes { get; }

        private NetworkRecvObject(RecvType recvType, uint peerId, string peerIp, ushort peerPort, byte[]? bytes)
        {
            RecvType = recvType;
            PeerID = peerId;
            PeerIP = peerIp;
            PeerPort = peerPort;
            Bytes = bytes;
        }



        /// <summary>
        /// Factory responsible for creating NetworkRecvObjects. Each method in this class corresponds
        ///  to one RecvType.
        /// </summary>
        internal static class Factory
        {
            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'connect' ENet event. Requires only
            ///  peer information (no byte[] payload).
            /// </summary>
            /// <param name="peerId"> ID of peer that just connected. </param>
            /// <param name="peerIp"> IP address of peer that just connected. </param>
            /// <param name="peerPort"> Port of peer that just connected. </param>
            /// <returns> The newly created 'connect' NetworkRecvObject. </returns>
            internal static NetworkRecvObject CreateFromConnect(uint peerId, string peerIp, ushort peerPort)
            {
                return new NetworkRecvObject(RecvType.Connect, peerId, peerIp, peerPort, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'disconnect' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="peerId"> ID of peer that just disconnected. </param>
            /// <param name="peerIp"> IP address of peer that just disconnected. </param>
            /// <param name="peerPort"> Port of peer that just disconnected. </param>
            /// <returns> The newly created 'disconnect' NetworkRecvObject. </returns>
            internal static NetworkRecvObject CreateFromDisconnect(uint peerId, string peerIp, ushort peerPort)
            {
                return new NetworkRecvObject(RecvType.Disconnect, peerId, peerIp, peerPort, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'timeout' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="peerId"> ID of peer that just timed out. </param>
            /// <param name="peerIp"> IP address of peer that just timed out. </param>
            /// <param name="peerPort"> Port of peer that just timed out. </param>
            /// <returns> The newly created 'timeout' NetworkRecvObject. </returns>
            internal static NetworkRecvObject CreateFromTimeout(uint peerId, string peerIp, ushort peerPort)
            {
                return new NetworkRecvObject(RecvType.Timeout, peerId, peerIp, peerPort, null);
            }

            /// <summary>
            /// Creates and returns a new NetworkRecvObject from a 'message' ENet event. Requires
            ///  peer information and byte[] payload of incoming message packet.
            /// </summary>
            /// <param name="peerId"> ID of peer that the message was received from. </param>
            /// <param name="peerIp"> IP address of peer that the message was received from. </param>
            /// <param name="peerPort"> Port of peer that the message was received from. </param>
            /// <param name="bytes"> The incoming message packet payload as byte[]. </param>
            /// <returns> The newly created 'message' NetworkRecvObject. </returns>
            internal static NetworkRecvObject CreateFromMessage(uint peerId, string peerIp, ushort peerPort, byte[] bytes)
            {
                return new NetworkRecvObject(RecvType.Message, peerId, peerIp, peerPort, bytes);
            }
        }
    }
}
