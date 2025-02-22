using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.DataObjects
{
    internal class NetworkSendDataObject
    {
        public uint PeerID { get; }
        public byte[] Bytes { get; }
        public SendType SendType { get; }

        private NetworkSendDataObject(NetworkSendDataObject.Builder builder)
        {
            PeerID = builder.peerId;
            Bytes = builder.bytes;
            SendType = builder.sendType;
        }



        public class Builder
        {
            internal uint peerId;
            internal byte[] bytes = [];
            internal SendType sendType;

            public Builder()
            {
                // Default constructor
            }



            public Builder AddPeerID(uint peerId)
            {
                this.peerId = peerId;
                return this;
            }

            public Builder AddBytes(byte[] bytes)
            {
                this.bytes = bytes;
                return this;
            }

            public Builder AddSendType(SendType sendType)
            {
                this.sendType = sendType;
                return this;
            }

            public NetworkSendDataObject Build()
            {
                return new NetworkSendDataObject(this);
            }
        }
    }
}
