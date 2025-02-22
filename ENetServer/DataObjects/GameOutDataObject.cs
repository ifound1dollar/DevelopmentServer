using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.DataObjects
{
    /// <summary>
    /// Data object containing NON-SERIALIZED game data TO BE SENT over the network. Must use Builder to create objects.
    /// </summary>
    public class GameOutDataObject
    {
        public uint PeerID { get; }
        public string TempDataString { get; }
        public SendType SendType { get; }

        private GameOutDataObject(GameOutDataObject.Builder builder)
        {
            PeerID = builder.PeerID;
            TempDataString = builder.TempDataString;
            SendType = builder.SendType;
        }



        /// <summary>
        /// Builder used to create new GameOutDataObject instances.
        /// </summary>
        public class Builder
        {
            internal uint PeerID { get; private set; }
            internal string TempDataString { get; private set; } = string.Empty;
            internal SendType SendType { get; private set; }

            public Builder()
            {
                // Default constructor
            }



            public Builder AddPeerID(uint peerId)
            {
                PeerID = peerId;
                return this;
            }

            public Builder AddTempDataString(string tempDataString)
            {
                TempDataString = tempDataString;
                return this;
            }

            public Builder AddSendType(SendType sendType)
            {
                SendType = sendType;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new GameOutDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed GameOutDataObject. </returns>
            public GameOutDataObject Build()
            {
                return new GameOutDataObject(this);
            }
        }



        #region Static Ease-of-Use Methods

        /// <summary>
        /// Creates a generic 'disconnect all' GameOutDataObject.
        /// </summary>
        /// <returns> The created GameOutDataObject. </returns>
        public static GameOutDataObject MakeGenericDisconnectAll()
        {
            GameOutDataObject dataObject = new Builder()
                .AddPeerID(0)                           // Not necessary here
                .AddTempDataString("")                  // Not necessary here
                .AddSendType(SendType.DISCONNECT_ALL)
                .Build();
            return dataObject;
        }

        /// <summary>
        /// Creates a generic 'message all' GameOutDataObject with the passed-in message string.
        /// </summary>
        /// <param name="message"> Message to send to all connected clients. </param>
        /// <returns> The created GameOutDataObject. </returns>
        public static GameOutDataObject MakeGenericMessageAll(string message)
        {
            GameOutDataObject dataObject = new Builder()
                .AddPeerID(0)                           // Not necessary here
                .AddTempDataString(message)
                .AddSendType (SendType.MESSAGE_ALL)
                .Build();
            return dataObject;
        }

        #endregion
    }
}
