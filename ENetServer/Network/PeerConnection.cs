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

        /// <summary>
        /// Creates a new PeerConnection object with the passed-in Peer, and creates a
        ///  corresponding Connection object according to the Peer data.
        /// </summary>
        /// <param name="peer"> Existing Peer from new connection. </param>
        /// <param name="isServer"> Whether the new Connection object to create is for a server. </param>
        internal PeerConnection(Peer peer, bool isServer)
        {
            Peer = peer;
            Connection = new(Peer.ID, peer.IP, peer.Port, DateTime.Now, isServer);
        }

        /// <summary>
        /// Creates a new PeerConnection object with predefined Peer and Connection objects.
        /// </summary>
        /// <param name="peer"> Existing Peer. </param>
        /// <param name="connection"> Existing Connection. </param>
        internal PeerConnection(Peer peer, Connection connection)
        {
            Peer = peer;
            Connection = connection;
        }
    }
}
