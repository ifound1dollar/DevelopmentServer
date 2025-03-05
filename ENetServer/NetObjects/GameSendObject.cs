using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.NetObjects
{
    public class GameSendObject
    {
        public SendType SendType { get; }
        public uint PeerID { get; }
        public GameDataObject? GameDataObject { get; }

        private GameSendObject(GameSendObject.Builder builder)
        {
            PeerID = builder.PeerID;
            SendType = builder.SendType;
            GameDataObject = builder.GameDataObject;
        }



        /// <summary>
        /// Builder used to create new GameSendObject instances.
        /// </summary>
        public class Builder
        {
            public SendType SendType { get; private set; }
            public uint PeerID { get; private set; }
            public GameDataObject? GameDataObject { get; private set; }

            public Builder()
            {
                // Default constructor
            }



            public Builder ForDisconnectOne(uint peerId)
            {
                SendType = SendType.Disconnect_One;
                PeerID = peerId;
                return this;
            }

            public Builder ForDisconnectAll()
            {
                SendType = SendType.Disconnect_All;
                return this;
            }

            public Builder ForMessageOne(uint peerId, GameDataObject? gameDataObject)
            {
                SendType = SendType.Message_One;
                PeerID = peerId;
                GameDataObject = gameDataObject;
                return this;
            }

            public Builder ForMessageAll(GameDataObject? gameDataObject)
            {
                SendType = SendType.Message_All;
                GameDataObject = gameDataObject;
                return this;
            }

            public Builder ForMessageAllExcept(uint peerId, GameDataObject? gameDataObject)
            {
                SendType = SendType.Message_AllExcept;
                PeerID = peerId;
                GameDataObject = gameDataObject;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new GameSendObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed GameSendObject. </returns>
            public GameSendObject Build()
            {
                return new GameSendObject(this);
            }
        }
    }
}
