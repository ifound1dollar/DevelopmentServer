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
    internal class NetworkSendObject
    {
        internal SendType SendType { get; }
        internal uint PeerID { get; }
        internal byte[] Bytes { get; }

        private NetworkSendObject(NetworkSendObject.Builder builder)
        {
            SendType = builder.SendType;
            PeerID = builder.PeerID;
            Bytes = builder.Bytes;
        }



        /// <summary>
        /// Builder used to create new NetworkSendDataObject instances.
        /// </summary>
        internal class Builder
        {
            internal SendType SendType { get; private set; }
            internal uint PeerID { get; private set; }
            internal byte[] Bytes { get; private set; } = [];

            internal Builder()
            {
                // Default constructor
            }



            internal Builder ForDisconnectOne(uint peerId)
            {
                SendType = SendType.Disconnect_One;
                PeerID = peerId;
                return this;
            }

            internal Builder ForDisconnectAll()
            {
                SendType = SendType.Disconnect_All;
                return this;
            }

            internal Builder ForMessageOne(uint peerId, byte[] bytes)
            {
                SendType = SendType.Message_One;
                PeerID = peerId;
                Bytes = bytes;
                return this;
            }

            internal Builder ForMessageAll(byte[] bytes)
            {
                SendType = SendType.Message_All;
                Bytes = bytes;
                return this;
            }

            internal Builder ForMessageAllExcept(uint peerId, byte[] bytes)
            {
                SendType = SendType.Message_AllExcept;
                PeerID = peerId;
                Bytes = bytes;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new NetworkSendDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed NetworkSendDataObject. </returns>
            internal NetworkSendObject Build()
            {
                return new NetworkSendObject(this);
            }
        }
    }
}
