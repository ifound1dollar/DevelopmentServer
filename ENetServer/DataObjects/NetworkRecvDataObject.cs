using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.DataObjects
{
    /// <summary>
    /// Data object containing SERIALIZED network data JUST RECEIVED over the network. Must use Builder to create objects.
    /// </summary>
    internal class NetworkRecvDataObject
    {
        public uint PeerID { get; }
        public string PeerIP { get; }
        public ushort PeerPort { get; }
        public byte[] Bytes { get; }
        public RecvType RecvType { get; }

        private NetworkRecvDataObject(NetworkRecvDataObject.Builder builder)
        {
            PeerID = builder.PeerID;
            PeerIP = builder.PeerIP;
            PeerPort = builder.PeerPort;
            Bytes = builder.Bytes;
            RecvType = builder.RecvType;
        }



        /// <summary>
        /// Builder used to create new NetworkRecvDataObject instances.
        /// </summary>
        public class Builder
        {
            internal uint PeerID { get; private set; }
            internal string PeerIP { get; private set; } = string.Empty;
            internal ushort PeerPort { get; private set; }
            internal byte[] Bytes { get; private set; } = [];
            internal RecvType RecvType { get; private set; }

            public Builder()
            {
                // Default constructor
            }



            public Builder AddPeerID(uint peerId)
            {
                PeerID = peerId;
                return this;
            }

            public Builder AddPeerIP(string peerIp)
            {
                PeerIP = peerIp;
                return this;
            }

            public Builder AddPeerPort(ushort peerPort)
            {
                PeerPort = peerPort;
                return this;
            }

            public Builder AddBytes(byte[] bytes)
            {
                Bytes = bytes;
                return this;
            }

            public Builder AddRecvType(RecvType recvType)
            {
                RecvType = recvType;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new NetworkRecvDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed NetworkRecvDataObject. </returns>
            public NetworkRecvDataObject Build()
            {
                return new NetworkRecvDataObject(this);
            }
        }
    }
}
