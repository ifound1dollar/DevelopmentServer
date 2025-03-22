using ENet;
using ENetServer.NetObjects;
using ENetServer.Network;
using System;
using System.Collections.Concurrent;
using static ENetServer.NetStatics;

namespace ENetServer.Management
{
    /// <summary>
    /// Encapsulates all ENet operations (start, stop, run) and Host data within this class.
    /// </summary>
    internal class Server
    {
        // HOST DATA
        private Host? serverHost;
        private Address address;
        private int peerLimit;
        private int channelLimit;

        // QUEUE REFERENCES
        private readonly ConcurrentQueue<NetSendObject> netSendQueue;
        private readonly ConcurrentQueue<NetRecvObject> netRecvQueue;

        /// <summary>
        /// Constructs a Server object with references to network concurrent queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Server(ConcurrentQueue<NetSendObject> netSendQueue,
            ConcurrentQueue<NetRecvObject> netRecvQueue)
        {
            this.netSendQueue = netSendQueue;
            this.netRecvQueue = netRecvQueue;
        }



        /// <summary>
        /// Map of all connected clients in form ID:PeerConnection.
        /// </summary>
        private Dictionary<uint, PeerConnection> Peers { get; set; } = new();

        internal Address GetAddress()
        {
            return address;
        }



        #region Setup / Start / Stop / Run Operations

        /// <summary>
        /// Sets parameters for the host server. All have default values.
        /// </summary>
        /// <param name="ip"> IP address the host will run on (irrelevant for server). </param>
        /// <param name="port"> Port that the host will listen on. </param>
        /// <param name="peerLimit"> Maximum number of peers that can be connected at once (library limits to 4096). </param>
        /// <param name="channelLimit"> Maximum number of channels that can be used for communication with this host. </param>
        internal void SetHostParameters(string ip = "127.0.0.1", ushort port = 7777, int peerLimit = 64, int channelLimit = 2)
        {
            // Address for this server host.
            address = new();
            address.SetIP(ip);          // Can optionally set specific IP to run on (not required for server)
            address.Port = port;        // Actual port server will run on

            // Server creation parameters.
            this.peerLimit = peerLimit;
            this.channelLimit = channelLimit;
        }

        /// <summary>
        /// Starts up server to begin listening on the designated port.
        /// </summary>
        internal void Start()
        {
            // Create server Host with address and args.
            serverHost = new();
            serverHost.Create(address, peerLimit, channelLimit, 0u, 0u, 1024 * 1024);   // 1024*1024 is maximum buffer size in enet.h
        }

        /// <summary>
        /// Stops server, performing last-minute operations (i.e. disconnecting all clients) before disposing host.
        /// </summary>
        internal void Stop()
        {
            // Useless check, just to silence warning (will never be null because is set within Start() method).
            if (serverHost == null) return;

            // Disconnect all clients before disposing server (graceful disconnects).
            QueueDisconnectAll();

            // Wait 3 seconds for clients to response to disconnect request.
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

            // Finally, flush and dispose server host.
            serverHost.Flush();
            serverHost.Dispose();
        }

        /// <summary>
        /// Handles net send tasks - read from network send queue, do ENet tasks (send, disconnect, etc.).
        /// </summary>
        internal void DoNetSendTasks()
        {
            // TODO: REMOVE THIS TEMP Connection
            Connection connection = new(uint.MaxValue, "0.0.0.0", ushort.MaxValue);

            // Loop until network send queue is empty.
            while (!netSendQueue.IsEmpty)
            {
                // Try to dequeue item from serializeQueue, operating on the item if successful.
                if (!netSendQueue.TryDequeue(out NetSendObject? netSendObject)) break;

                // Operate based on send type.
                switch (netSendObject.SendType)
                {
                    case SendType.Disconnect_One:
                        {
                            QueueDisconnectOne(netSendObject);
                            break;
                        }
                    case SendType.Disconnect_All:
                        {
                            QueueDisconnectAll();
                            break;
                        }
                    case SendType.Message_One:
                        {
                            QueueSendOne(netSendObject);
                            break;
                        }
                    case SendType.Message_All:
                        {
                            QueueSendAll(netSendObject);
                            break;
                        }
                    case SendType.Message_AllExcept:
                        {
                            QueueSendAllExcept(netSendObject);
                            break;
                        }
                    case SendType.TestSend:
                        {
                            // TODO: REMOVE THIS TEST CASE
                            Peers.GetValueOrDefault(netSendObject.PeerID);  // SIMULATE GETTING PEER FOR ACTUAL SEND

                            if (netSendObject.Bytes != null)
                            {
                                NetRecvObject netRecvObject = NetRecvObject.Factory.CreateFromTestRecv(
                                    connection, netSendObject.Bytes);
                                netRecvQueue.Enqueue(netRecvObject);
                            }
                            break;
                        }
                    // DO NOTHING FOR DEFAULT CASE
                }

                // Return the dequeued NetSendObject to the static object pool.
                //NetSendObject.Factory.ReturnToPool(netSendObject);
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
            if (serverHost == null) return;

            Event netEvent;

            while (true)
            {
                // HANDLE QUEUED EVENTS HERE. Returns 0 if no events remain, -1 if error.
                if (serverHost.CheckEvents(out netEvent) <= 0)  // Only moves on once all pending events have been handled.
                {
                    // DISPATCH NEW EVENTS HERE. Returns 1 if an event was dispatched.
                    if (serverHost.Service(0, out netEvent) <= 0)
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

        #region Send Methods

        /// <summary>
        /// Sends a packet to a single peer.
        /// </summary>
        /// <param name="peerId"> ID of Peer to send packet to. </param>
        /// <param name="packet"> Packet to send to Peer. </param>
        internal void QueueSendOne(NetSendObject dataObject)
        {
            // Return if a peer with this ID does not exist.
            if (!Peers.TryGetValue(dataObject.PeerID, out PeerConnection? peerConnection)) return;

            Peer peer = peerConnection.Peer;

            // Verify that the peer is valid.
            if (peer.State != PeerState.Connected) return;

            // Create packet from passed-in byte[], which is already in ready-to-send format.
            Packet packet = default;
            packet.Create(dataObject.Bytes);

            // TEMP send on channel 0.
            peer.Send(0, ref packet);
        }

        /// <summary>
        /// Sends a packet to all connected peers.
        /// </summary>
        /// <param name="packet"> Packet to send to Peer. </param>
        internal void QueueSendAll(NetSendObject dataObject)
        {
            // Iterate over all clients, sending packet to all except matching.
            foreach (var peerConnection in Peers)
            {
                Peer peer = peerConnection.Value.Peer;

                // Verify that the peer is valid.
                if (peer.State != PeerState.Connected) continue;

                // Create packet from passed-in byte[], which is already in ready-to-send format.
                Packet packet = default;
                packet.Create(dataObject.Bytes);

                // TEMP send on channel 0.
                peer.Send(0, ref packet);
            }
        }

        /// <summary>
        /// Sends a packet to all connected peers except one.
        /// </summary>
        /// <param name="peerId"> ID of Peer being excluded from packet send. </param>
        /// <param name="packet"> Packet to send to Peer. </param>
        internal void QueueSendAllExcept(NetSendObject dataObject)
        {
            // Iterate over all clients, sending packet to all except matching.
            foreach (var peerConnection in Peers)
            {
                Peer peer = peerConnection.Value.Peer;

                // Verify that the peer is valid.
                if (peer.State != PeerState.Connected) continue;

                // Skip peer passed in as argument.
                if (peerConnection.Key == dataObject.PeerID) continue;

                // Create packet from passed-in byte[], which is already in ready-to-send format.
                Packet packet = default;
                packet.Create(dataObject.Bytes);

                // TEMP send on channel 0.
                peer.Send(0, ref packet);
            }
        }

        #endregion
        #region Disconnect Methods

        /// <summary>
        /// Queues disconnect requests to be sent to all connected clients next tick.
        /// </summary>
        /// <param name="peerId"> ID of Peer to disconnect. </param>
        internal void QueueDisconnectOne(NetSendObject dataObject)
        {
            // Return if a Peer with this ID is not found.
            if (!Peers.TryGetValue(dataObject.PeerID, out PeerConnection? peerConnection)) return;

            Peer peer = peerConnection.Peer;

            // Verify that the peer is valid.
            if (peer.State != PeerState.Connected) return;

            // Disconnect with default data value.
            peer.Disconnect(0);
        }

        /// <summary>
        /// Queues disconnect requests to be sent to all connected clients next tick.
        /// </summary>
        internal void QueueDisconnectAll()
        {
            // Iterate over all clients, sending packet to all except matching.
            foreach (var peerConnection in Peers)
            {
                Peer peer = peerConnection.Value.Peer;

                // Verify that the peer is valid.
                if (peer.State != PeerState.Connected) continue;

                // Disconnect with default data value.
                peer.Disconnect(0);
            }
        }

        #endregion

        #region Receive Event Handlers

        private void HandleConnectEvent(ref Event connectEvent)
        {
            // Create new PeerConnection and add to Peers map.
            PeerConnection peerConnection = new(connectEvent.Peer);
            Peers.Add(peerConnection.Peer.ID, peerConnection);

            // Enqueue connect object with new peer's Connection for use by other threads.
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(peerConnection.Connection);
            netRecvQueue.Enqueue(dataObject);
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            // Remove PeerConnection from Peers map and store in temp variable, checking for success.
            if (!Peers.Remove(disconnectEvent.Peer.ID, out PeerConnection? peerConnection)) return;
            
            // Enqueue disconnect object with disconnected peer's Connection for use by other threads.
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(peerConnection.Connection);
            netRecvQueue.Enqueue(dataObject);
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent)
        {
            // Remove PeerConnection from Peers map and store in temp variable, checking for success.
            if (!Peers.Remove(timeoutEvent.Peer.ID, out PeerConnection? peerConnection)) return;

            // Enqueue timeout object with timed-out peer's Connection for use by other threads.
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(peerConnection.Connection);
            netRecvQueue.Enqueue(dataObject);
        }

        private void HandleReceiveEvent(ref Event receiveEvent)
        {
            // Copy packet payload into byte[].
            byte[] bytes = new byte[receiveEvent.Packet.Length];
            receiveEvent.Packet.CopyTo(bytes);

            // Enqueue NetRecvObject with this peer's Connection ONLY IF an entry for this peer exists in Peers.
            if (Peers.TryGetValue(receiveEvent.Peer.ID, out PeerConnection? peerConnection))
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
