using ENet;
using ENetServer.Network;
using ENetServer.NetObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;
using System.IO.Pipes;

namespace ENetServer.Network
{
    internal class Client
    {
        // HOST DATA
        private Host? clientHost;
        private Address address;
        private int channelLimit;

        // QUEUE REFERENCES
        private readonly ConcurrentQueue<NetSendObject> netSendQueue;
        private readonly ConcurrentQueue<NetRecvObject> netRecvQueue;

        /// <summary>
        /// Constructs a Client object with references to network concurrent queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Client(ConcurrentQueue<NetSendObject> netSendQueue,
            ConcurrentQueue<NetRecvObject> netRecvQueue)
        {
            this.netSendQueue = netSendQueue;
            this.netRecvQueue = netRecvQueue;
        }



        /// <summary>
        /// Map of all servers this client is connected to in form ID:PeerConnection.
        /// </summary>
        private Dictionary<uint, PeerConnection> Connections { get; } = new();
        private Peer PrimaryServerPeer { get; set; }

        internal Address GetAddress()
        {
            return address;
        }



        #region Setup / Start / Stop / Run Operations

        /// <summary>
        /// Sets parameters for the host client. All have default values.
        /// </summary>
        /// <param name="ip"> IP address of this host will run on (useless?). </param>
        /// <param name="port"> Port this host will run on. </param>
        /// <param name="channelLimit"> Maximum number of channels that can be used for communication with this host. </param>
        internal void SetHostParameters(string ip = "127.0.0.1", ushort port = 8888, int channelLimit = 2)
        {
            address = new();
            address.SetIP(ip);
            address.Port = port;

            this.channelLimit = channelLimit;
        }

        /// <summary>
        /// Starts up client host, but not yet connected to server.
        /// </summary>
        internal void Start()
        {
            // Create the client host with address, 1 connection (server only), defined channel limit, no bandwidth limit.
            clientHost = new();
            clientHost.Create(address, 1, channelLimit, 0u, 0u, 1024*1024);
        }

        /// <summary>
        /// Stops client, sending a disconnect request and waiting 3 seconds for successful disconnect ACKs.
        /// </summary>
        internal void Stop()
        {
            // Useless check, just to silence warning (will never be null because is set within Start() method).
            if (clientHost == null) return;

            // Disconnect all Connections and wait 3 seconds for ACKs.
            DisconnectAllOnStop(clientHost);

            // Finally, flush and dispose client host.
            clientHost.Flush();
            clientHost.Dispose();
        }

        /// <summary>
        /// Disconnects all Connections, then runs ENet service for 3 seconds to wait for ACKs (blocks).
        /// </summary>
        /// <param name="serverHost"> Non-null Host used only to silence warning. </param>
        private void DisconnectAllOnStop(Host serverHost)
        {
            // Disconnect all peers before disposing server (graceful disconnects).
            foreach (var peerConnection in Connections)
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Value.Peer;
                if (peer.State != PeerState.Connected) continue;

                // Disconnect with default data value.
                peer.Disconnect(0);
            }

            // Wait 3 seconds for clients to respond to disconnect request.
            while (serverHost.Service(3000, out Event netEvent) > 0)
            {
                switch (netEvent.Type)
                {
                    case EventType.Disconnect:
                        HandleDisconnectEvent(ref netEvent);
                        break;

                    case EventType.Receive:
                        netEvent.Packet.Dispose();  // Dispose any incoming packets
                        break;
                }
            }
        }


        /// <summary>
        /// Handles net send tasks - read from network send queue, do ENet tasks (send, disconnect, etc.).
        /// </summary>
        internal void DoNetSendTasks()
        {
            // Loop until network send queue is empty.
            while (!netSendQueue.IsEmpty)
            {
                // Try to dequeue item from serializeQueue, operating on the item if successful.
                if (!netSendQueue.TryDequeue(out NetSendObject? netSendObject)) break;

                // Operate based on send type.
                switch (netSendObject.SendType)
                {
                    case SendType.Connect_One:
                        {
                            QueueConnectOne(netSendObject);
                            break;
                        }
                    case SendType.Disconnect_One:
                        {
                            QueueDisconnectOne(netSendObject);
                            break;
                        }
                    case SendType.Disconnect_All:
                        {
                            QueueDisconnectAll(netSendObject);
                            break;
                        }
                    case SendType.Message_One:
                        {
                            QueueMessageOne(netSendObject);
                            break;
                        }
                    case SendType.Message_All:
                        {
                            QueueMessageAll(netSendObject);
                            break;
                        }
                    // DO NOT INCLUDE MESSAGEALLEXCEPT CASE (not necessary on client)
                    case SendType.TestSend:
                        {
                            // TODO: REMOVE THIS TEST CASE
                            bool set = PrimaryServerPeer.IsSet;    // SIMULATE CHECKING CONNECTION FOR ACTUAL SEND

                            if (netSendObject.Bytes != null)
                            {
                                NetRecvObject netRecvObject = NetRecvObject.Factory.CreateFromTestRecv(
                                    netSendObject.Connection, netSendObject.Bytes);
                                netRecvQueue.Enqueue(netRecvObject);
                            }
                            break;
                        }
                    // DO NOTHING FOR DEFAULT CASE
                }

                /* - outside of switch case, inside while loop - */
            }
        }

        /// <summary>
        /// Handles net receive tasks - handles ENet events and runs host service, adds to network receive queue.
        /// </summary>
        internal void DoNetReceiveTasks()
        {
            // Each loop iteration (event poll/dispatch), checks for new events. This
            //  loop continues iterating until 'polled' is set to true, which only occurs when
            //  the CheckEvents() method returns nothing; it will only return nothing when there
            //  are no more events to handle. THIS MEANS THAT ALL QUEUED EVENTS WILL BE HANDLED
            //  FIRST WITHIN THE LOOP ITERATION.

            // If NO events exist (returns 0), runs host service to dispatch events and
            //  sets 'polled' to true.
            // NOTE: The 'polled' variable being true indicates that all events have already
            //  been handled and that the inner loop should be exited after the host service
            //  has dispatched any new events.
            //      If host service does NOT dispatch an event (returns 0), no work remains
            //       so break from inner loop and return to main loop (avoids reaching switch
            //       statement with null event).
            //      If host service DOES dispatch any events, switch on event type and handle
            //       event data accordingly (AFAIK it dispatches numerous events, so I'm not
            //       sure which event will be handled in the switch in this scenario).
            // If events DO exist, continue to switch statement to handle the event data.

            // Useless check, just to silence warning (will never be null because is set within Start() method).
            if (clientHost == null) return;

            Event netEvent;

            while (true)
            {
                // HANDLE QUEUED EVENTS HERE. Returns 0 if no events remain, -1 if error.
                if (clientHost.CheckEvents(out netEvent) <= 0)  // Only moves on once all pending events have been handled.
                {
                    // DISPATCH NEW EVENTS HERE. Returns 1 if an event was dispatched.
                    if (clientHost.Service(0, out netEvent) <= 0)
                        break;  // Breaks from loop ONLY once CheckEvents() and Service() have no work remaining.
                }

                // Handle event populated by either CheckEvents() or Service().
                switch (netEvent.Type)
                {
                    case EventType.None:
                        break;

                    case EventType.Connect:
                        HandleConnectEvent(ref netEvent);
                        break;

                    case EventType.Disconnect:
                        HandleDisconnectEvent(ref netEvent);
                        break;

                    case EventType.Timeout:
                        HandleTimeoutEvent(ref netEvent);
                        break;

                    case EventType.Receive:
                        HandleReceiveEvent(ref netEvent);
                        break;
                }
            }
        }


        #endregion

        #region Connect/Disconnect Methods

        /// <summary>
        /// Queues connect request to be sent to one server next tick.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant server data. </param>
        internal void QueueConnectOne(NetSendObject netSendObject)
        {
            // Verify not trying to connect to a Host already connected to.
            string ip = netSendObject.Connection.IP;
            foreach (var peerConnection in Connections)
            {
                if (peerConnection.Value.Connection.IP == ip)
                {
                    Console.WriteLine("[ERROR] Attempted to connect to an existing Connection. Aborting.");
                    return;
                }
            }

            // Create Address object with IP and Port from NetSendObject.
            Address remoteAddress = new();
            remoteAddress.SetIP(netSendObject.Connection.IP);
            remoteAddress.Port = netSendObject.Connection.Port;

            // Queue connect to remote address.
            try
            {
                clientHost?.Connect(remoteAddress);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Queues disconnect request to be sent to one server next tick.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant server data. </param>
        internal void QueueDisconnectOne(NetSendObject netSendObject)
        {
            // Only if a peer with this ID is found.
            if (Connections.TryGetValue(netSendObject.Connection.ID, out PeerConnection? peerConnection))
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Peer;
                if (peer.State != PeerState.Connected) return;

                // Disconnect with default data value.
                peer.Disconnect(0u);
            }
        }

        /// <summary>
        /// Queues disconnect requests to be sent to all connected servers next tick.
        /// </summary>
        internal void QueueDisconnectAll(NetSendObject netSendObject)
        {
            // Iterate over all servers, sending disconnect request to all that are connected.
            foreach (var peerConnection in Connections)
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Value.Peer;
                if (peer.State != PeerState.Connected) continue;

                // Disconnect with default data value.
                peer.Disconnect(0u);
            }
        }

        #endregion
        #region Send Methods

        /// <summary>
        /// Sends a packet to a single peer.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant server and payload data. </param>
        internal void QueueMessageOne(NetSendObject netSendObject)
        {
            // Only if a peer with this ID is found.
            if (Connections.TryGetValue(netSendObject.Connection.ID, out PeerConnection? peerConnection))
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Peer;
                if (peer.State != PeerState.Connected) return;

                // Create packet from passed-in byte[], which is already in ready-to-send format.
                Packet packet = default;
                packet.Create(netSendObject.Bytes);

                // TEMP send on channel 0.
                peer.Send(0, ref packet);
            }
        }

        /// <summary>
        /// Sends a packet to all connected peers.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant payload data. </param>
        internal void QueueMessageAll(NetSendObject netSendObject)
        {
            // Iterate over all servers, sending packet to all except matching.
            foreach (var peerConnection in Connections)
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Value.Peer;
                if (peer.State != PeerState.Connected) continue;

                // Create packet from passed-in byte[], which is already in ready-to-send format.
                Packet packet = default;
                packet.Create(netSendObject.Bytes);

                // TEMP send on channel 0.
                peer.Send(0, ref packet);
            }
        }

        #endregion

        #region Receive Event Handlers

        private void HandleConnectEvent(ref Event connectEvent)
        {
            // Create new PeerConnection and add to map. New connections as client are always servers.
            PeerConnection peerConnection = new(connectEvent.Peer, true);
            Connections.Add(peerConnection.Peer.ID, peerConnection);

            // Enqueue connect object with new peer's Connection for use by other threads.
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(peerConnection.Connection);
            netRecvQueue.Enqueue(dataObject);
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            // Remove PeerConnection from map and enqueue Connection if successful.
            if (Connections.Remove(disconnectEvent.Peer.ID, out PeerConnection? peerConnection))
            {
                // Enqueue disconnect object with disconnected peer's Connection for use by other threads.
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(peerConnection.Connection);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent)
        {
            // Remove PeerConnection from map and enqueue Connection if successful.
            if (Connections.Remove(timeoutEvent.Peer.ID, out PeerConnection? peerConnection))
            {
                // Enqueue timeout object with timed-out peer's Connection for use by other threads.
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(peerConnection.Connection);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleReceiveEvent(ref Event receiveEvent)
        {
            // Copy packet payload into byte[].
            byte[] bytes = new byte[receiveEvent.Packet.Length];
            receiveEvent.Packet.CopyTo(bytes);

            // Enqueue NetRecvObject with this peer's Connection ONLY IF an entry for this peer exists in map.
            if (Connections.TryGetValue(receiveEvent.Peer.ID, out PeerConnection? peerConnection))
            {
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromMessage(peerConnection.Connection, bytes);
                netRecvQueue.Enqueue(dataObject);
            }

            // Always dispose packet after handling receive, even if did not enqueue NetRecvObject.
            receiveEvent.Packet.Dispose();
        }

        #endregion

    }
}
