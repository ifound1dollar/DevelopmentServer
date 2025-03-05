using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.NetObjects
{
    public class GameRecvObject
    {
        public uint PeerID { get; }
        public RecvType RecvType { get; }
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
            public RecvType RecvType { get; private set; }
            public GameDataObject? GameDataObject { get; private set; }

            public Builder()
            {
                // Default constructor
            }



            public Builder FromConnect(uint peerId)
            {
                RecvType = RecvType.Connect;
                PeerID = peerId;
                return this;
            }

            public Builder FromDisconnect(uint peerId)
            {
                RecvType = RecvType.Disconnect;
                PeerID = peerId;
                return this;
            }

            public Builder FromTimeout(uint peerId)
            {
                RecvType = RecvType.Timeout;
                PeerID = peerId;
                return this;
            }

            public Builder FromMessage(uint peerId, GameDataObject? gameDataObject)
            {
                RecvType = RecvType.Message;
                PeerID = peerId;
                GameDataObject = gameDataObject;
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
