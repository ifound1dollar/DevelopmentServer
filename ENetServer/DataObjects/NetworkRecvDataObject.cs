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
        internal uint PeerID { get; }
        internal string PeerIP { get; }
        internal ushort PeerPort { get; }
        internal byte[] Bytes { get; }
        internal RecvType RecvType { get; }

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
        internal class Builder
        {
            internal uint PeerID { get; private set; }
            internal string PeerIP { get; private set; } = string.Empty;
            internal ushort PeerPort { get; private set; }
            internal byte[] Bytes { get; private set; } = [];
            internal RecvType RecvType { get; private set; }

            internal Builder()
            {
                // Default constructor
            }



            internal Builder AddPeerID(uint peerId)
            {
                PeerID = peerId;
                return this;
            }

            internal Builder AddPeerIP(string peerIp)
            {
                PeerIP = peerIp;
                return this;
            }

            internal Builder AddPeerPort(ushort peerPort)
            {
                PeerPort = peerPort;
                return this;
            }

            internal Builder AddBytes(byte[] bytes)
            {
                Bytes = bytes;
                return this;
            }

            internal Builder AddRecvType(RecvType recvType)
            {
                RecvType = recvType;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new NetworkRecvDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed NetworkRecvDataObject. </returns>
            internal NetworkRecvDataObject Build()
            {
                return new NetworkRecvDataObject(this);
            }
        }
    }
}
