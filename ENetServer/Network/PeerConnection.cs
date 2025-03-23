using ENet;
using ENetServer.Network;
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

        internal PeerConnection(Peer peer, bool isServer)
        {
            Peer = peer;
            Connection = new(Peer.ID, peer.IP, peer.Port, DateTime.Now, isServer);
        }

        internal PeerConnection(Peer peer, Connection connection)
        {
            Peer = peer;
            Connection = connection;
        }
    }
}
