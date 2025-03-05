using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects
{
    public class GameRecvObject
    {
        public uint PeerID { get; }
        public NetHelpers.RecvType RecvType { get; }
        public GameDataObject? GameDataObject { get; }

        private GameRecvObject(GameRecvObject.Builder builder)
        {
            PeerID = builder.PeerID;
            RecvType = builder.RecvType;
            GameDataObject = builder.GameDataObject;
        }



        /// <summary>
        /// Builder used to create new GameRecvObject instances.
        /// </summary>
        public class Builder
        {
            public uint PeerID { get; private set; }
            public NetHelpers.RecvType RecvType { get; private set; }
            public GameDataObject? GameDataObject { get; private set; }

            public Builder()
            {
                // Default constructor
            }



            public Builder AddPeerID(uint peerId)
            {
                PeerID = peerId;
                return this;
            }

            public Builder AddRecvType(NetHelpers.RecvType recvType)
            {
                RecvType = recvType;
                return this;
            }

            public Builder AddGameDataObject(GameDataObject? dataObject)
            {
                GameDataObject = dataObject;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new GameRecvObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed GameRecvObject. </returns>
            public GameRecvObject Build()
            {
                return new GameRecvObject(this);
            }
        }
    }
}
