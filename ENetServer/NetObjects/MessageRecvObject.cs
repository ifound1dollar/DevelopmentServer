using ENetServer.NetObjects.DataObjects;
using ENetServer.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    public class MessageRecvObject
    {
        public RecvType RecvType { get; }
        public Connection Connection { get; }
        public byte[]? Bytes { get; set; }
        public GameDataObject? GameDataObject { get; set; }

        private MessageRecvObject(RecvType recvType, Connection connection, byte[] bytes)
        {
            RecvType = recvType;
            Connection = connection;
            Bytes = bytes;
        }



        /// <summary>
        /// Factory used to create MessageRecvObjects. Works with serializable message objects.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Creates and returns a new MessageRecvObject from a 'message' ENet event. Requires
            ///  peer information and byte[] payload of incoming message packet.
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that message was received from. </param>
            /// <param name="bytes"> The incoming message packet payload as byte[]. </param>
            /// <returns> The newly created 'message' MessageRecvObject. </returns>
            internal static MessageRecvObject CreateFromMessage(Connection connection, byte[] bytes)
            {
                return new MessageRecvObject(RecvType.Message, connection, bytes);
            }



            /// <summary>
            /// Creates and returns a new TEST MessageRecvObject, which was not actually received over the
            ///  but was simply re-queued by the network thread.
            /// </summary>
            /// <param name="connection"> TEST Connection to simulate message receive overhead. </param>
            /// <param name="bytes"> TEST byte[] to simulate message receive overhead. </param>
            /// <returns> The newly created TEST MessageRecvObject. </returns>
            internal static MessageRecvObject CreateFromTestRecv(Connection connection, byte[] bytes)
            {
                return new MessageRecvObject(RecvType.TestRecv, connection, bytes);
            }
        }
    }
}
