using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Data object containing SERIALIZED network data JUST RECEIVED over the network. Must use Builder to create objects.
    /// </summary>
    internal class NetworkRecvObject
    {
        internal uint PeerID { get; }
        internal string PeerIP { get; }
        internal ushort PeerPort { get; }
        internal byte[] Bytes { get; }
        internal RecvType RecvType { get; }

        private NetworkRecvObject(NetworkRecvObject.Builder builder)
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



            public Builder FromConnect(uint peerId, string peerIp, ushort peerPort)
            {
                RecvType = RecvType.Connect;
                PeerID = peerId;
                PeerIP = peerIp;
                PeerPort = peerPort;
                return this;
            }

            public Builder FromDisconnect(uint peerId, string peerIp, ushort peerPort)
            {
                RecvType = RecvType.Disconnect;
                PeerID = peerId;
                PeerIP = peerIp;
                PeerPort = peerPort;
                return this;
            }

            public Builder FromTimeout(uint peerId, string peerIp, ushort peerPort)
            {
                RecvType = RecvType.Timeout;
                PeerID = peerId;
                PeerIP = peerIp;
                PeerPort = peerPort;
                return this;
            }

            public Builder FromMessage(uint peerId, string peerIp, ushort peerPort, byte[] bytes)
            {
                RecvType = RecvType.Message;
                PeerID = peerId;
                PeerIP = peerIp;
                PeerPort = peerPort;
                Bytes = bytes;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new NetworkRecvDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed NetworkRecvDataObject. </returns>
            internal NetworkRecvObject Build()
            {
                return new NetworkRecvObject(this);
            }
        }
    }
}
