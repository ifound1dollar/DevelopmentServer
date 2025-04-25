using ENet;
using ENetServer.Network;
using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Net object containing NON-SERIALIZED network data TO BE SENT over the network.
    ///  Use GameSendObject.Factory to create objects.
    /// </summary>
    public class GameSendObject
    {
        public SendType SendType { get; }
        public PeerParams PeerParams { get; }
        public bool Reliable { get; }
        public uint Data { get; }
        public GameDataObject? GameDataObject { get; }
        

        private GameSendObject(SendType sendType, PeerParams peerParams)
        {
            SendType = sendType;
            PeerParams = peerParams;
            // GameDataObject remains null and Data remains 0.
        }

        private GameSendObject(SendType sendType, PeerParams peerParams, uint data)
        {
            SendType = sendType;
            PeerParams = peerParams;
            Data = data;
            // GameDataObject remains null.
        }

        private GameSendObject(SendType sendType, PeerParams peerParams, GameDataObject? gameDataObject)
        {
            SendType = sendType;
            PeerParams = peerParams;
            GameDataObject = gameDataObject;
            // Data remains 0.
        }



        /// <summary>
        /// Factory responsible for creating GameSendObjects. Each creator method corresponds to
        ///  one SendType.
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
            /// Creates and returns a new GameSendObject for connecting to one remote host.
            /// </summary>
            /// <param name="ip"> IP address of peer attempting to connect to. </param>
            /// <param name="port"> Port of peer attempting to connect to. </param>
            /// <param name="token"> Login Token to send to the Peer to connect to. </param>
            /// <returns> The newly created 'connect one' GameSendObject. </returns>
            public static GameSendObject CreateConnectOne(string ip, ushort port, string token)
            {
                PeerParams peerParams = new(ip, port, token);
                return new GameSendObject(SendType.Connect_One, peerParams);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for disconnecting [from] one remote host.
            /// </summary>
            /// <param name="id"> ID of peer to disconnect [from]. </param>
            /// <param name="data"> Data uint to send with disconnect request. </param>
            /// <returns> The newly created 'disconnect one' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectOne(uint id, uint data)
            {
                PeerParams peerParams = new(id);
                return new GameSendObject(SendType.Disconnect_One, peerParams, data);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for disconnecting [from] many remote hosts.
            /// </summary>
            /// <param name="idArray"> Array of peer IDs of hosts to disconnect [from]. </param>
            /// <param name="data"> Data uint to send with disconnect request. </param>
            /// <returns></returns>
            public static GameSendObject CreateDisconnectMany(uint[] idArray, uint data)
            {
                PeerParams peerParams = new(idArray);
                return new GameSendObject(SendType.Disconnect_Many, peerParams, data);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for disconnecting [from] all remote hosts.
            /// </summary>
            /// <param name="hostType"> HostType that all peers must match (can be Both, should not be None). </param>
            /// <param name="data"> Data uint to send with disconnect request. </param>
            /// <returns> The newly created 'disconnect all' GameSendObject. </returns>
            public static GameSendObject CreateDisconnectAll(HostType hostType, uint data)
            {
                PeerParams hostParams = new(hostType);
                return new GameSendObject(SendType.Disconnect_All, hostParams, data);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging one remote host.
            /// </summary>
            /// <param name="id"> ID of peer to message. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message one' GameSendObject. </returns>
            public static GameSendObject CreateMessageOne(uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(id);
                return new GameSendObject(SendType.Message_One, peerParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging many remote hosts.
            /// </summary>
            /// <param name="idArray"> Array of peer IDs of hosts to message. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns></returns>
            public static GameSendObject CreateMessageMany(uint[] idArray, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(idArray);
                return new GameSendObject(SendType.Message_Many, peerParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging all remote hosts.
            /// </summary>
            /// <param name="hostType"> HostType all peers must match (can be Both, should not be None). </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all' GameSendObject. </returns>
            public static GameSendObject CreateMessageAll(HostType hostType, GameDataObject gameDataObject)
            {
                PeerParams hostParams = new(hostType);
                return new GameSendObject(SendType.Message_All, hostParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging all remote hosts
            ///  except one.
            /// </summary>
            /// <param name="hostType"> HostType all peers must match (can be Both, should not be None). </param>
            /// <param name="id"> ID of host to ignore. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all except' GameSendObject. </returns>
            public static GameSendObject CreateMessageAllExcept(HostType hostType, uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(hostType, id);
                return new GameSendObject(SendType.Message_AllExcept, peerParams, gameDataObject);
            }



            /// <summary>
            /// Creates and returns a new TEST GameSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="shouldSend"> TEST whether the TestSend message will actually be sent to a peer. </param>
            /// <param name="id"> TEST peer ID to simulate message send overhead. </param>
            /// <param name="gameDataObject"> TEST GameDataObject to simulate message send overhead. </param>
            /// <returns> The newly created TEST GameSendObject. </returns>
            public static GameSendObject CreateTestSend(bool shouldSend, uint id, GameDataObject gameDataObject)
            {
                // Set HostType to Both if should actually send, else None.
                PeerParams peerParams = (shouldSend) ? new(HostType.Both, id) : new(HostType.None, id);
                return new GameSendObject(SendType.TestSend, peerParams, gameDataObject);
            }

        }
    }
}
