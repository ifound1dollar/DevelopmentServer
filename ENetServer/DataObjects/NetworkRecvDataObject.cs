using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.DataObjects
{
    internal class NetworkRecvDataObject
    {
        public uint PeerID { get; }
        public string PeerIP { get; }
        public ushort PeerPort { get; }
        public byte[] Bytes { get; }
        public RecvType RecvType { get; }

        private NetworkRecvDataObject(NetworkRecvDataObject.Builder builder)
        {
            PeerID = builder.peerId;
            PeerIP = builder.peerIp;
            PeerPort = builder.peerPort;
            Bytes = builder.bytes;
            RecvType = builder.recvType;
        }



        public class Builder
        {
            internal uint peerId;
            internal string peerIp = "";
            internal ushort peerPort;
            internal byte[] bytes = [];
            internal RecvType recvType;

            public Builder()
            {
                // Default constructor
            }



            public Builder AddPeerID(uint peerId)
            {
                this.peerId = peerId;
                return this;
            }

            public Builder AddPeerIP(string peerIp)
            {
                this.peerIp = peerIp;
                return this;
            }

            public Builder AddPeerPort(ushort peerPort)
            {
                this.peerPort = peerPort;
                return this;
            }

            public Builder AddBytes(byte[] bytes)
            {
                this.bytes = bytes;
                return this;
            }

            public Builder AddRecvType(RecvType recvType)
            {
                this.recvType = recvType;
                return this;
            }

            public NetworkRecvDataObject Build()
            {
                return new NetworkRecvDataObject(this);
            }
        }
    }
}
