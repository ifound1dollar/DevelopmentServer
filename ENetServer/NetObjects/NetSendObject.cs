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

        public NetSendObject(SendType sendType, PeerParams peerParams)
        {
            SendType = sendType;
            PeerParams = peerParams;
            // Bytes remains null.
        }

        public NetSendObject(SendType sendType, PeerParams peerParams, byte[] bytes)
        {
            SendType = sendType;
            PeerParams = peerParams;
            Bytes = bytes;
        }

    }
}
