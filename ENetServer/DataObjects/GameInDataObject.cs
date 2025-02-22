using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetworkManager;

namespace ENetServer.DataObjects
{
    /// <summary>
    /// Data object containing NON-SERIALIZED game data JUST RECEIVED from the network. Must use Builder to create objects.
    /// </summary>
    public class GameInDataObject
    {
        public uint PeerID { get; }
        public string TempDataString { get; }

        private GameInDataObject(GameInDataObject.Builder builder)
        {
            PeerID = builder.PeerID;
            TempDataString = builder.TempDataString;
        }



        /// <summary>
        /// Builder used to create new GameInDataObject instances.
        /// </summary>
        public class Builder
        {
            internal uint PeerID { get; private set; }
            internal string TempDataString { get; private set; } = string.Empty;

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

            /// <summary>
            /// Constructs and returns a new GameInDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed GameInDataObject. </returns>
            public GameInDataObject Build()
            {
                return new GameInDataObject(this);
            }
        }
    }
}
