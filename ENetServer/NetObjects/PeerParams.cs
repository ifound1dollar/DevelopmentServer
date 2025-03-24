using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Objects of this class contain various Peer-relevant data required for send operations. New
    ///  objects are created by NetObjects and should not typically be manually created by users.
    /// </summary>
    public class PeerParams
    {
        public HostType HostType { get; }
        public uint ID { get; }
        public uint[] IDArray { get; }
        public string IP { get; }
        public ushort Port { get; }



        /// <summary>
        /// Constructs a PeerParams object with Connect_One data.
        /// </summary>
        /// <param name="ip"> IP address of peer attempting to connect to. </param>
        /// <param name="port"> Port of peer attempting to connect to. </param>
        internal PeerParams(string ip, ushort port)
        {
            IP = ip;
            Port = port;

            // Initialize unused reference types.
            IDArray = [];
        }

        /// <summary>
        /// Constructs a PeerParams object with Disconnect_One OR Message_One data.
        /// </summary>
        /// <param name="id"> ID of peer attempting to disconnect or message. </param>
        internal PeerParams(uint id)
        {
            ID = id;

            // Initialize unused reference types.
            IP = string.Empty;
            IDArray = [];
        }

        /// <summary>
        /// Constructs a PeerParams object with Disconnect_All OR Message_All data.
        /// </summary>
        /// <param name="hostType"> HostType that all peers must match (can be Both). </param>
        internal PeerParams(HostType hostType)
        {
            HostType = hostType;

            // Initialize unused reference types.
            IP = string.Empty;
            IDArray = [];
        }

        /// <summary>
        /// Constructs a PeerParams object with Message_AllExcept data.
        /// </summary>
        /// <param name="hostType"> HostType that all peers to message must match. </param>
        /// <param name="id"> ID of peer to ignore. </param>
        internal PeerParams(HostType hostType, uint id)
        {
            HostType = hostType;
            ID = id;

            // Initialize unused reference types.
            IP = string.Empty;
            IDArray = [];
        }

        /// <summary>
        /// Constructs a PeerParams object with Disconnect_Many OR Message_Many data.
        /// </summary>
        /// <param name="idArray"> Array of peer IDs to disconnect or message. </param>
        internal PeerParams(uint[] idArray)
        {
            IDArray = idArray;

            // Initialize unused reference types.
            IP = string.Empty;
        }
    }
}
