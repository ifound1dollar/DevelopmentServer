using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects
{
    public class GameSendObject
    {
        public NetHelpers.SendType SendType { get; }
        public uint PeerID { get; }
        public GameDataObject? GameDataObject { get; }

        private GameSendObject(GameSendObject.Builder builder)
        {
            PeerID = builder.PeerID;
            SendType = builder.SendType;
            GameDataObject = builder.GameDataObject;
        }



        // TODO: REDO BUILDER TO HAVE IT ACCEPT FIVE FUNCTIONS FOR EACH SEND TYPE, INSTEAD OF ALLOWING ADDING
        //  DIFFERENT DATA POINTS. ex. AsDisconnectOne()/ForDisconnectOne()/WHATEVER



        /// <summary>
        /// Builder used to create new GameSendObject instances.
        /// </summary>
        public class Builder
        {
            public NetHelpers.SendType SendType { get; private set; }
            public uint PeerID { get; private set; }
            public GameDataObject? GameDataObject { get; private set; }

            public Builder()
            {
                // Default constructor
            }



            public Builder AddSendType(NetHelpers.SendType sendType)
            {
                SendType = sendType;
                return this;
            }

            public Builder AddPeerID(uint peerId)
            {
                PeerID = peerId;
                return this;
            }

            public Builder AddGameDataObject(GameDataObject? dataObject)
            {
                GameDataObject = dataObject;
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



        #region Static Template Methods

        /// <summary>
        /// Creates a GameSendObject for a 'disconnect one' action.
        /// </summary>
        /// <param name="peerId"> ID of peer to disconnect. </param>
        /// <returns> The created 'disconnect one' GameSendObject. </returns>
        public static GameSendObject MakeDisconnectOne(uint peerId)
        {
            GameSendObject sendObject = new GameSendObject.Builder()
                .AddSendType(NetHelpers.SendType.Disconnect_One)
                .AddPeerID(peerId)
                .Build();
            return sendObject;
        }

        /// <summary>
        /// Creates a GameSendObject for a 'disconnect all' action.
        /// </summary>
        /// <returns> The created 'disconnect all' GameSendObject. </returns>
        public static GameSendObject MakeDisconnectAll()
        {
            GameSendObject sendObject = new GameSendObject.Builder()
                .AddSendType(NetHelpers.SendType.Disconnect_All)
                .Build();
            return sendObject;
        }

        /// <summary>
        /// Creates a GameSendObject for a 'message one' action.
        /// </summary>
        /// <param name="peerId"> ID of peer to send message to. </param>
        /// <param name="dataObject"> GameDataObject containing data to send as message. </param>
        /// <returns> The created 'message one' GameSendObject. </returns>
        public static GameSendObject MakeMessageOne(uint peerId, GameDataObject dataObject)
        {
            GameSendObject sendObject = new GameSendObject.Builder()
                .AddSendType(NetHelpers.SendType.Message_One)
                .AddPeerID(peerId)
                .AddGameDataObject(dataObject)
                .Build();
            return sendObject;
        }

        /// <summary>
        /// Creates a GameSendObject for a 'message all' action.
        /// </summary>
        /// <param name="dataObject"> GameDataObject containing data to send as message. </param>
        /// <returns> The created 'message all' GameSendObject. </returns>
        public static GameSendObject MakeMessageAll(GameDataObject dataObject)
        {
            GameSendObject sendObject = new GameSendObject.Builder()
                .AddSendType(NetHelpers.SendType.Message_All)
                .AddGameDataObject(dataObject)
                .Build();
            return sendObject;
        }

        #endregion
    }
}
