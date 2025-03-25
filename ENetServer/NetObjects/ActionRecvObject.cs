using ENetServer.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    public class ActionRecvObject
    {
        public RecvType RecvType { get; }
        public Connection Connection { get; }

        private ActionRecvObject(RecvType recvType, Connection connection)
        {
            RecvType = recvType;
            Connection = connection;
        }



        /// <summary>
        /// Factory used to create ActionRecvObjects. For non-message objects.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Creates and returns a new ActionRecvObject from a 'connect' ENet event. Requires only
            ///  peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just connected. </param>
            /// <returns> The newly created 'connect' ActionRecvObject. </returns>
            internal static ActionRecvObject CreateFromConnect(Connection connection)
            {
                return new ActionRecvObject(RecvType.Connect, connection);
            }

            /// <summary>
            /// Creates and returns a new ActionRecvObject from a 'disconnect' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just disconnected. </param>
            /// <returns> The newly created 'disconnect' ActionRecvObject. </returns>
            internal static ActionRecvObject CreateFromDisconnect(Connection connection)
            {
                return new ActionRecvObject(RecvType.Disconnect, connection);
            }

            /// <summary>
            /// Creates and returns a new ActionRecvObject from a 'timeout' ENet event. Requires
            ///  only peer information (no byte[] payload).
            /// </summary>
            /// <param name="connection"> Connection object corresponding to peer that just timed out. </param>
            /// <returns> The newly created 'timeout' ActionRecvObject. </returns>
            internal static ActionRecvObject CreateFromTimeout(Connection connection)
            {
                return new ActionRecvObject(RecvType.Timeout, connection);
            }
        }
    }
}
