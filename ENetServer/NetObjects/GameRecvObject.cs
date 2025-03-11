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
    /// Net object containing DESERIALIZED network data JUST RECEIVED over the network. Must use Factory to create objects.
    /// </summary>
    public class GameRecvObject
    {
        public uint PeerID { get; }
        public RecvType RecvType { get; }
        public GameDataObject? GameDataObject { get; }

        private GameRecvObject(RecvType recvType, uint peerId, GameDataObject? gameDataObject)
        {
            PeerID = peerId;
            RecvType = recvType;
            GameDataObject = gameDataObject;
        }



        /// <summary>
        /// Factory responsible for creating GameRecvObjects. Each method in this class corresponds to
        ///  one RecvType.
        /// </summary>
        internal static class Factory
        {
            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'connect' ENet event. Requires only
            ///  peer information, does not require a deserialized GameDataObject.
            /// </summary>
            /// <param name="peerId"> ID of peer that just connected. </param>
            /// <returns> The newly created 'connect' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromConnect(uint peerId)
            {
                return new GameRecvObject(RecvType.Connect, peerId, null);
            }

            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'disconnect' ENet event. Requires only
            ///  peer information, does not require a deserialized GameDataObject.
            /// </summary>
            /// <param name="peerId"> ID of peer that just disconnected. </param>
            /// <returns> The newly created 'disconnect' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromDisconnect(uint peerId)
            {
                return new GameRecvObject(RecvType.Disconnect, peerId, null);
            }

            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'timeout' ENet event. Requires only
            ///  peer information, does not require a deserialized GameDataObject.
            /// </summary>
            /// <param name="peerId"> ID of peer that just timed out. </param>
            /// <returns> The newly created 'timeout' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromTimeout(uint peerId)
            {
                return new GameRecvObject(RecvType.Timeout, peerId, null);
            }

            /// <summary>
            /// Creates and returns a new GameRecvObject from a 'message' ENet event. Requires
            ///  peer information and a valid non-null deserialized GameDataObject.
            /// </summary>
            /// <param name="peerId"> ID of peer that the message was received from. </param>
            /// <param name="gameDataObject"> GameDataObject deserialized from the received byte[] payload. Must not be null. </param>
            /// <returns> The newly created 'message' GameRecvObject. </returns>
            internal static GameRecvObject CreateFromMessage(uint peerId, GameDataObject gameDataObject)
            {
                return new GameRecvObject(RecvType.Message, peerId, gameDataObject);
            }
        }
    }
}
