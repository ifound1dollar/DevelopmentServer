using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    public class MessageSendObject
    {
        public MessageType MessageType { get; }
        public PeerParams PeerParams { get; }
        public GameDataObject? GameDataObject { get; set; }
        public byte[]? Bytes { get; set; }

        private MessageSendObject(MessageType messageType, PeerParams peerParams, GameDataObject gameDataObject)
        {
            MessageType = messageType;
            PeerParams = peerParams;
            GameDataObject = gameDataObject;
        }



        /// <summary>
        /// Factory responsible for creating MessageSendObjects. Each creator method corresponds to
        ///  one RecvType.
        /// </summary>
        public static class Factory
        {
            // NOTE: Using dedicated Factory methods for each SendType enforces safe object
            //  creation and ensures that there will not be a mismatch between the intended
            //  send operation and the object's actual SendType.
            // Additionally, taking raw data as arguments rather than a pre-constructed
            //  PeerParams object further reinforces this design principle, as the wrong
            //  parameters cannot be sent for the intended SendType (ex. passing a Connect-
            //  ready PeerParams object with a Disconnect_One SendType).

            /// <summary>
            /// Creates and returns a new MessageSendObject for messaging one remote host.
            /// </summary>
            /// <param name="id"> ID of peer to message. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message one' MessageSendObject. </returns>
            public static MessageSendObject CreateMessageOne(uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(id);
                return new MessageSendObject(MessageType.Message_One, peerParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new MessageSendObject for messaging many remote hosts.
            /// </summary>
            /// <param name="idArray"> Array of peer IDs of hosts to message. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns></returns>
            public static MessageSendObject CreateMessageMany(uint[] idArray, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(idArray);
                return new MessageSendObject(MessageType.Message_Many, peerParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new MessageSendObject for messaging all remote hosts.
            /// </summary>
            /// <param name="hostType"> HostType all peers must match (can be Both, should not be None). </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all' MessageSendObject. </returns>
            public static MessageSendObject CreateMessageAll(HostType hostType, GameDataObject gameDataObject)
            {
                PeerParams hostParams = new(hostType);
                return new MessageSendObject(MessageType.Message_All, hostParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new MessageSendObject for messaging all remote hosts
            ///  except one.
            /// </summary>
            /// <param name="hostType"> HostType all peers must match (can be Both, should not be None). </param>
            /// <param name="id"> ID of host to ignore. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all except' MessageSendObject. </returns>
            public static MessageSendObject CreateMessageAllExcept(HostType hostType, uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(hostType, id);
                return new MessageSendObject(MessageType.Message_All_Except, peerParams, gameDataObject);
            }



            /// <summary>
            /// Creates and returns a new TEST MessageSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="id"> TEST peer ID to simulate message send overhead. </param>
            /// <param name="gameDataObject"> TEST GameDataObject to simulate message send overhead. </param>
            /// <returns> The newly created TEST MessageSendObject. </returns>
            public static MessageSendObject CreateTestSend(uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(id);
                return new MessageSendObject(MessageType.TestMessage, peerParams, gameDataObject);
            }

        }
    }
}
