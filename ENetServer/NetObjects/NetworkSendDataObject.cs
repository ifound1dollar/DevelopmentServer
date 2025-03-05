using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Data object containing SERIALIZED network data TO BE SENT over the network. Must use Builder to create objects.
    /// </summary>
    internal class NetworkSendDataObject
    {
        internal uint PeerID { get; }
        internal byte[] Bytes { get; }
        internal SendType SendType { get; }

        private NetworkSendDataObject(NetworkSendDataObject.Builder builder)
        {
            PeerID = builder.PeerID;
            Bytes = builder.Bytes;
            SendType = builder.SendType;
        }



        /// <summary>
        /// Builder used to create new NetworkSendDataObject instances.
        /// </summary>
        internal class Builder
        {
            internal uint PeerID { get; private set; }
            internal byte[] Bytes { get; private set; } = [];
            internal SendType SendType { get; private set; }

            internal Builder()
            {
                // Default constructor
            }



            internal Builder AddPeerID(uint peerId)
            {
                PeerID = peerId;
                return this;
            }

            internal Builder AddBytes(byte[] bytes)
            {
                Bytes = bytes;
                return this;
            }

            internal Builder AddSendType(SendType sendType)
            {
                SendType = sendType;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new NetworkSendDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed NetworkSendDataObject. </returns>
            internal NetworkSendDataObject Build()
            {
                return new NetworkSendDataObject(this);
            }
        }
    }
}
