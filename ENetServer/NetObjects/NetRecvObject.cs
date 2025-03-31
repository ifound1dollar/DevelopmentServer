using ENetServer.Network;
using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Net object containing SERIALIZED network data JUST RECEIVED over the network.
    ///  Use NetRecvObject.Factory to create objects.
    /// </summary>
    internal class NetRecvObject
    {
        internal RecvType RecvType { get; }
        internal PeerParams PeerParams { get; }
        internal uint Data { get; }
        internal byte[]? Bytes { get; }
        internal int Length { get; }

        private NetRecvObject(RecvType recvType, PeerParams peerParams, uint data)
        {
            RecvType = recvType;
            PeerParams = peerParams;
            Data = data;
            // Bytes remains null and Length remains 0.
        }

        private NetRecvObject(RecvType recvType, PeerParams peerParams, byte[]? bytes, int length)
        {
            RecvType = recvType;
            PeerParams = peerParams;
            Bytes = bytes;
            Length = length;
            // Data remains 0.
        }



        /// <summary>
        /// Factory responsible for creating NetRecvObjects. Each creator method corresponds to
        ///  one RecvType.
        /// </summary>
        internal static class Factory
        {
            // NOTE: Each of the below Factory methods are almost identical (generally violating
            //  the DRY principle) and could reasonably be consolidated into two methods: those
            //  with payload data, and those without.
            // However, using separate Factory methods for each RecvType enforces safe object
            //  creation and guarantees that the receive event being handled will be accurately
            //  represented within the NetObject. It eliminates the possibility of a mismatch
            //  between the event type and the stored RecvType (ex. a Message receive event that
            //  has a null payload reference).

            /// <summary>
            /// Creates and returns a new NetRecvObject from a 'connect' ENet event. Requires only
            ///  peer information (no byte[] payload).
            /// </summary>
            /// <param name="peerParams"> PeerParams object corresponding to peer that just connected. </param>
            /// <param name="data"> Data uint from connect event. </param>
            /// <returns> The newly created 'connect' NetRecvObject. </returns>
            internal static NetRecvObject CreateFromConnect(PeerParams peerParams, uint data)
            {
                return new NetRecvObject(RecvType.Connect, peerParams, data);
            }

            /// <summary>
            /// Creates and returns a new NetRecvObject from a 'disconnect' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="peerParams"> PeerParams object corresponding to peer that just disconnected. </param>
            /// <param name="data"> Data uint from disconnect event. </param>
            /// <returns> The newly created 'disconnect' NetRecvObject. </returns>
            internal static NetRecvObject CreateFromDisconnect(PeerParams peerParams, uint data)
            {
                return new NetRecvObject(RecvType.Disconnect, peerParams, data);
            }

            /// <summary>
            /// Creates and returns a new NetRecvObject from a 'timeout' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="peerParams"> PeerParams object corresponding to peer that just timed out. </param>
            /// <param name="data"> Data uint from timeout event. </param>
            /// <returns> The newly created 'timeout' NetRecvObject. </returns>
            internal static NetRecvObject CreateFromTimeout(PeerParams peerParams, uint data)
            {
                return new NetRecvObject(RecvType.Timeout, peerParams, data);
            }

            /// <summary>
            /// Creates and returns a new NetRecvObject from a 'message' ENet event. Requires
            ///  peer information and byte[] payload of incoming message packet.
            /// </summary>
            /// <param name="peerParams"> PeerParams object corresponding to peer that message was received from. </param>
            /// <param name="bytes"> The incoming message packet payload as byte[]. </param>
            /// <param name="length"> The length of the actual data in the byte[] payload. </param>
            /// <returns> The newly created 'message' NetRecvObject. </returns>
            internal static NetRecvObject CreateFromMessage(PeerParams peerParams, byte[] bytes, int length)
            {
                return new NetRecvObject(RecvType.Message, peerParams, bytes, length);
            }
        }
    }
}
