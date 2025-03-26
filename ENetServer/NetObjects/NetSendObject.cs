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

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Net object containing SERIALIZED network data TO BE SENT over the network.
    ///  Use NetSendObject.Factory to create objects.
    /// </summary>
    public class NetSendObject
    {
        public SendType SendType { get; }
        public PeerParams PeerParams { get; }
        //public bool IsSerializable { get; }
        public GameDataObject? GameDataObject { get; set; }
        public byte[]? Bytes { get; set; }

        private NetSendObject(SendType sendType, PeerParams peerParams)
        {
            SendType = sendType;
            PeerParams = peerParams;
            //IsSerializable = false;
            // GameDataObject remains null.
        }

        private NetSendObject(SendType sendType, PeerParams peerParams, GameDataObject gameDataObject)
        {
            SendType = sendType;
            PeerParams = peerParams;
            //IsSerializable = true;
            GameDataObject = gameDataObject;
        }



        /// <summary>
        /// Factory responsible for creating NetSendObjects. Each Factory method corresponds to
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
            /// <returns> The newly created 'connect one' GameSendObject. </returns>
            public static NetSendObject CreateConnectOne(string ip, ushort port)
            {
                PeerParams peerParams = new(ip, port);
                return new NetSendObject(SendType.Connect_One, peerParams);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for disconnecting [from] one remote host.
            /// </summary>
            /// <param name="id"> ID of peer to disconnect [from]. </param>
            /// <returns> The newly created 'disconnect one' GameSendObject. </returns>
            public static NetSendObject CreateDisconnectOne(uint id)
            {
                PeerParams peerParams = new(id);
                return new NetSendObject(SendType.Disconnect_One, peerParams);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for disconnecting [from] many remote hosts.
            /// </summary>
            /// <param name="idArray"> Array of peer IDs of hosts to disconnect [from]. </param>
            /// <returns></returns>
            public static NetSendObject CreateDisconnectMany(uint[] idArray)
            {
                PeerParams peerParams = new(idArray);
                return new NetSendObject(SendType.Disconnect_Many, peerParams);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for disconnecting [from] all remote hosts.
            /// </summary>
            /// <param name="hostType"> HostType that all peers must match (can be Both, should not be None). </param>
            /// <returns> The newly created 'disconnect all' GameSendObject. </returns>
            public static NetSendObject CreateDisconnectAll(HostType hostType)
            {
                PeerParams hostParams = new(hostType);
                return new NetSendObject(SendType.Disconnect_All, hostParams);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging one remote host.
            /// </summary>
            /// <param name="id"> ID of peer to message. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message one' GameSendObject. </returns>
            public static NetSendObject CreateMessageOne(uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(id);
                return new NetSendObject(SendType.Message_One, peerParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging many remote hosts.
            /// </summary>
            /// <param name="idArray"> Array of peer IDs of hosts to message. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns></returns>
            public static NetSendObject CreateMessageMany(uint[] idArray, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(idArray);
                return new NetSendObject(SendType.Message_Many, peerParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging all remote hosts.
            /// </summary>
            /// <param name="hostType"> HostType all peers must match (can be Both, should not be None). </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all' GameSendObject. </returns>
            public static NetSendObject CreateMessageAll(HostType hostType, GameDataObject gameDataObject)
            {
                PeerParams hostParams = new(hostType);
                return new NetSendObject(SendType.Message_All, hostParams, gameDataObject);
            }

            /// <summary>
            /// Creates and returns a new GameSendObject for messaging all remote hosts
            ///  except one.
            /// </summary>
            /// <param name="hostType"> HostType all peers must match (can be Both, should not be None). </param>
            /// <param name="id"> ID of host to ignore. </param>
            /// <param name="gameDataObject"> GameDataObject containing message contents. Must not be null. </param>
            /// <returns> The newly created 'message all except' GameSendObject. </returns>
            public static NetSendObject CreateMessageAllExcept(HostType hostType, uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(hostType, id);
                return new NetSendObject(SendType.Message_AllExcept, peerParams, gameDataObject);
            }



            /// <summary>
            /// Creates and returns a new TEST GameSendObject, which is not sent over the network but
            ///  instead is re-queued by the network thread.
            /// </summary>
            /// <param name="id"> TEST peer ID to simulate message send overhead. </param>
            /// <param name="gameDataObject"> TEST GameDataObject to simulate message send overhead. </param>
            /// <returns> The newly created TEST GameSendObject. </returns>
            public static NetSendObject CreateTestSend(uint id, GameDataObject gameDataObject)
            {
                PeerParams peerParams = new(id);
                return new NetSendObject(SendType.TestSend, peerParams, gameDataObject);
            }

        }
    }
}
