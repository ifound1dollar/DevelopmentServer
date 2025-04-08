using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network
{
    public class PeerData
    {
        public enum CustomState { None, Initiated, AwaitingToken, AwaitingAck, Connected, Disconnected }

        private Peer _peer;
        public ref Peer Peer => ref _peer;  // Use non-auto property to return Peer by reference.
        public DateTime ConnectTime { get; }
        public CustomState State { get; set; }

        public PeerData(Peer peer, CustomState state)
        {
            _peer = peer;
            State = state;

            ConnectTime = DateTime.UtcNow;
        }
    }
}
