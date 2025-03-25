using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    public class ActionSendObject
    {
        public ActionType ActionType { get; }
        public PeerParams PeerParams { get; }

        private ActionSendObject(ActionType actionType, PeerParams peerParams)
        {
            ActionType = actionType;
            PeerParams = peerParams;
        }



        /// <summary>
        /// Factory responsible for creating ActionSendObjects. Each creator method corresponds to
        ///  one SendType.
        /// </summary>
        public static class Factory
        {
            // NOTE: Using dedicated Factory methods for each ActionType enforces safe object
            //  creation and ensures that there will not be a mismatch between the intended
            //  send operation and the object's actual ActionType.
            // Additionally, taking raw data as arguments rather than a pre-constructed
            //  PeerParams object further reinforces this design principle, as the wrong
            //  parameters cannot be sent for the intended SendType (ex. passing a Connect-
            //  ready PeerParams object with a Disconnect_One SendType).

            /// <summary>
            /// Creates and returns a new ActionSendObject for connecting to one remote host.
            /// </summary>
            /// <param name="ip"> IP address of peer attempting to connect to. </param>
            /// <param name="port"> Port of peer attempting to connect to. </param>
            /// <returns> The newly created 'connect one' ActionSendObject. </returns>
            public static ActionSendObject CreateConnectOne(string ip, ushort port)
            {
                PeerParams peerParams = new(ip, port);
                return new ActionSendObject(ActionType.Connect_One, peerParams);
            }

            /// <summary>
            /// Creates and returns a new ActionSendObject for disconnecting [from] one remote host.
            /// </summary>
            /// <param name="id"> ID of peer to disconnect [from]. </param>
            /// <returns> The newly created 'disconnect one' ActionSendObject. </returns>
            public static ActionSendObject CreateDisconnectOne(uint id)
            {
                PeerParams peerParams = new(id);
                return new ActionSendObject(ActionType.Disconnect_One, peerParams);
            }

            /// <summary>
            /// Creates and returns a new ActionSendObject for disconnecting [from] many remote hosts.
            /// </summary>
            /// <param name="idArray"> Array of peer IDs of hosts to disconnect [from]. </param>
            /// <returns></returns>
            public static ActionSendObject CreateDisconnectMany(uint[] idArray)
            {
                PeerParams peerParams = new(idArray);
                return new ActionSendObject(ActionType.Disconnect_Many, peerParams);
            }

            /// <summary>
            /// Creates and returns a new ActionSendObject for disconnecting [from] all remote hosts.
            /// </summary>
            /// <param name="hostType"> HostType that all peers must match (can be Both, should not be None). </param>
            /// <returns> The newly created 'disconnect all' ActionSendObject. </returns>
            public static ActionSendObject CreateDisconnectAll(HostType hostType)
            {
                PeerParams hostParams = new(hostType);
                return new ActionSendObject(ActionType.Disconnect_All, hostParams);
            }
        }
    }
}
