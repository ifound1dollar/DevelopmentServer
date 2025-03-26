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
    /// NetObject for messages just received over the network. Use NetRecvObject.Factory to create objects.
    /// </summary>
    public class NetRecvObject
    {
        public RecvType RecvType { get; }
        public Connection Connection { get; }
        public bool IsDeserializable { get; }
        public byte[]? Bytes { get; set; }
        public GameDataObject? GameDataObject { get; set; }

        private NetRecvObject(RecvType recvType, Connection connection)
        {
            RecvType = recvType;
            Connection = connection;
            IsDeserializable = false;
            // Bytes remains null.
        }

        private NetRecvObject(RecvType recvType, Connection connection, byte[]? bytes)
        {
            RecvType = recvType;
            Connection = connection;
            IsDeserializable = true;
            Bytes = bytes;
        }



        /// <summary>
        /// Factory responsible for creating NetRecvObjects. Each creator method corresponds to
        ///  one RecvType.
        /// </summary>
        public static class Factory
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
            /// <param name="connection"> Connection object corresponding to peer that just connected. </param>
            /// <returns> The newly created 'connect' NetRecvObject. </returns>
            public static NetRecvObject CreateFromConnect(Connection connection)
            {
                return new NetRecvObject(RecvType.Connect, connection);
            }

            /// <summary>
            /// Creates and returns a new NetRecvObject from a 'disconnect' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just disconnected. </param>
            /// <returns> The newly created 'disconnect' NetRecvObject. </returns>
            public static NetRecvObject CreateFromDisconnect(Connection connection)
            {
                return new NetRecvObject(RecvType.Disconnect, connection);
            }

            /// <summary>
            /// Creates and returns a new NetRecvObject from a 'timeout' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just timed out. </param>
            /// <returns> The newly created 'timeout' NetRecvObject. </returns>
            public static NetRecvObject CreateFromTimeout(Connection connection)
            {
                return new NetRecvObject(RecvType.Timeout, connection);
            }

            /// <summary>
            /// Creates and returns a new NetRecvObject from a 'message' ENet event. Requires
            ///  peer information and byte[] payload of incoming message packet.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that message was received from. </param>
            /// <param name="bytes"> The incoming message packet payload as byte[]. </param>
            /// <returns> The newly created 'message' NetRecvObject. </returns>
            public static NetRecvObject CreateFromMessage(Connection connection, byte[] bytes)
            {
                return new NetRecvObject(RecvType.Message, connection, bytes);
            }



            /// <summary>
            /// Creates and returns a new TEST NetRecvObject, which was not actually received over the
            ///  but was simply re-queued by the network thread.
            /// </summary>
            /// <param name="connection"> TEST Connection to simulate message receive overhead. </param>
            /// <param name="bytes"> TEST byte[] to simulate message receive overhead. </param>
            /// <returns> The newly created TEST GameRecvObject. </returns>
            public static NetRecvObject CreateFromTestRecv(Connection connection, byte[] bytes)
            {
                return new NetRecvObject(RecvType.TestRecv, connection, bytes);
            }
        }
    }
}
