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
        internal bool Reliable { get; }
        internal uint Data { get; }
        internal byte[]? Bytes { get; }
        internal int Length { get; }

        public NetSendObject(SendType sendType, PeerParams peerParams, uint data)
        {
            SendType = sendType;
            PeerParams = peerParams;
            Data = data;
            // Bytes remains null and Length remains 0.
        }

        public NetSendObject(SendType sendType, PeerParams peerParams, byte[] bytes, int length)
        {
            SendType = sendType;
            PeerParams = peerParams;
            Bytes = bytes;
            Length = length;
            // Data remains 0 (only necessary for non-message sends).
        }

    }
}
