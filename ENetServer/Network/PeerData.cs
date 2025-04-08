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

        public Peer Peer { get; }
        public DateTime ConnectTime { get; }
        public CustomState State { get; set; }

        public PeerData(Peer peer, CustomState state)
        {
            Peer = peer;
            State = state;

            ConnectTime = DateTime.UtcNow;
        }
    }
}
