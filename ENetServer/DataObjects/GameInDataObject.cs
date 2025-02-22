using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetworkManager;

namespace ENetServer.DataObjects
{
    public class GameInDataObject
    {
        public uint PeerID { get; }
        public string TempDataString { get; }

        private GameInDataObject(GameInDataObject.Builder builder)
        {
            PeerID = builder.peerId;
            TempDataString = builder.tempDataString;
        }



        public class Builder
        {
            internal uint peerId;
            internal string tempDataString = "";

            public Builder()
            {
                // Default constructor
            }



            public Builder AddPeerID(uint peerId)
            {
                this.peerId = peerId;
                return this;
            }

            public Builder AddTempDataString(string tempDataString)
            {
                this.tempDataString = tempDataString;
                return this;
            }


            public GameInDataObject Build()
            {
                return new GameInDataObject(this);
            }
        }
    }
}
