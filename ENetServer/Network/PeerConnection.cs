using ENet;
using ENetServer.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network
{
    internal class PeerConnection
    {
        internal Peer Peer { get; }
        internal Connection Connection { get; }

        internal PeerConnection(Peer peer)
        {
            Peer = peer;
            Connection = new(Peer.ID, peer.IP, peer.Port);
        }
    }
}
