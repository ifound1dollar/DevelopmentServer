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
using System.Buffers;
using System.Collections;

namespace ENetServer.Network
{
    internal class Client
    {
        // HOST DATA
        private Host? clientHost;
        private Address address;
        private int peerLimit;
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
        /// Map of all servers this client is connected to in form ID:Peer.
        /// </summary>
        private Dictionary<uint, Peer> Servers { get; } = new();
        private Dictionary<uint, Peer> AllPeers { get; } = new();

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
        /// <param name="peerLimit"> Maximum number of peers that can be connected at once (library limits to 4096). </param>
        /// <param name="channelLimit"> Maximum number of channels that can be used for communication with this host. </param>
        internal void SetHostParameters(string ip, ushort port, int peerLimit, int channelLimit)
        {
            address = new();
            address.SetIP(ip);
            address.Port = port;

            this.peerLimit = peerLimit;
            this.channelLimit = channelLimit;
        }

        /// <summary>
        /// Starts up client host, but not yet connected to server.
        /// </summary>
        internal void Start()
        {
            // Create the client host with address, defined peer limit, defined channel limit, no bandwidth limit.
            clientHost = new();
            clientHost.Create(address, peerLimit, channelLimit, 0u, 0u, 1024*1024);

            // Prevent incoming connections (only outgoing connections allowed on clients).
            // Also disables connecting to another client.
            clientHost.PreventConnections(true);
        }

        /// <summary>
        /// Stops client, sending a disconnect request and waiting 3 seconds for successful disconnect ACKs.
        /// </summary>
        internal void Stop()
        {
            // Useless check, just to silence warning (will never be null because is set within Start() method).
            if (clientHost == null) return;

            // Disconnect all Clients and wait 3 seconds for ACKs.
            DisconnectAllOnStop(clientHost);

            // Finally, flush and dispose client host.
            clientHost.Flush();
            clientHost.Dispose();
        }

        /// <summary>
        /// Disconnects all Clients, then runs ENet service for 3 seconds to wait for ACKs (blocks).
        /// </summary>
        /// <param name="clientHost"> Non-null Host used only to silence warning. </param>
        private void DisconnectAllOnStop(Host clientHost)
        {
            // Disconnect all peers before disposing server (graceful disconnects).
            foreach (var peer in Servers)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // Disconnect with default data value.
                peer.Value.Disconnect(0);
            }

            // Wait 3 seconds for clients to respond to disconnect request.
            while (clientHost.Service(3000, out Event netEvent) > 0)
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
            // Store number of elements when this method is initially called, then dequeue that many.
            // This will prevent an infinite loop that could be encountered if items were being added
            //  to the queue as fast or faster than they were processed, which would cause the thread
            //  to get stuck dequeuing and never run ENet events.

            int queueCount = netSendQueue.Count;
            for (int i = 0; i < queueCount; i++)
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
                    case SendType.Disconnect_Many:
                        {
                            QueueDisconnectMany(netSendObject);
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
                    case SendType.Message_Many:
                        {
                            QueueMessageMany(netSendObject);
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
                            if (netSendObject.Bytes != null)
                            {
                                // If TestSend has no HostType, simply re-enqueue as received (no send).
                                if (netSendObject.PeerParams.HostType == HostType.None)
                                {
                                    NetRecvObject netRecvObject = NetRecvObject.Factory.CreateFromMessage(
                                        netSendObject.PeerParams, netSendObject.Bytes, netSendObject.Length);
                                    netRecvQueue.Enqueue(netRecvObject);
                                }
                                // Else if has a HostType, enqueue message to one.
                                else
                                {
                                    QueueMessageOne(netSendObject);
                                }
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
            string ip = netSendObject.PeerParams.IP;
            ushort port = netSendObject.PeerParams.Port;

            // Verify port is within valid range (outbound connections can only ever be to servers).
            if (port < ServerPortMin && port >= ClientPortMin)
            {
                Console.WriteLine("[ERROR] Specified port out of range for new Connect attempt. Valid range: {0}-{1}",
                    ServerPortMin, ClientPortMin - 1);
                return;
            }

            // Verify not trying to connect to a Host already connected to.
            foreach (var peer in Servers)
            {
                if (peer.Value.IP == ip && peer.Value.Port == port)
                {
                    Console.WriteLine("[ERROR] Attempted to connect to an existing Connection. Aborting.");
                    return;
                }
            }

            // Create Address object with IP and Port from NetSendObject.
            Address remoteAddress = new();
            remoteAddress.SetIP(netSendObject.PeerParams.IP);
            remoteAddress.Port = netSendObject.PeerParams.Port;

            // Queue connect to remote address.
            try
            {
                Peer? pendingPeer = clientHost?.Connect(remoteAddress, 2, netSendObject.Data);
                if (pendingPeer != null)
                {
                    pendingPeer.Value.Timeout(32, 5000, 10000); //32 and 5000 are default, last param default is 30000 (30s)
                    AllPeers[pendingPeer.Value.ID] = pendingPeer.Value;
                }
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
            if (Servers.TryGetValue(netSendObject.PeerParams.ID, out Peer peer))
            {
                // Verify that the peer is valid.
                if (peer.State != PeerState.Connected) return;

                // Disconnect with data value.
                peer.Disconnect(netSendObject.Data);
            }
        }

        /// <summary>
        /// Queues disconnect requests to all remote hosts with IDs contained in the NetSendObject.
        /// </summary>
        /// <param name="netSendObject">  </param>
        internal void QueueDisconnectMany(NetSendObject netSendObject)
        {
            // Iterate over IDArray and try to disconnect any valid Connection with the ID.
            foreach (var id in netSendObject.PeerParams.IDArray)
            {
                // Only if a peer with this ID is found.
                if (Servers.TryGetValue(id, out Peer peer))
                {
                    // Verify that the peer is valid.
                    if (peer.State != PeerState.Connected) return;

                    // Disconnect with data value.
                    peer.Disconnect(netSendObject.Data);
                }
            }
        }

        /// <summary>
        /// Queues disconnect requests to be sent to all connected remote hosts (servers).
        /// </summary>
        /// <param name="netSendObject">  </param>
        internal void QueueDisconnectAll(NetSendObject netSendObject)
        {
            // Do not filter based on HostType because as a client, all remote hosts will be servers.
            foreach (var peer in Servers)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // Disconnect with data value.
                peer.Value.Disconnect(netSendObject.Data);
            }
        }

        #endregion
        #region Send Methods

        /// <summary>
        /// Sends a packet to a single peer.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant peer and payload data. </param>        
        internal void QueueMessageOne(NetSendObject netSendObject)
        {
            // Create packet from passed-in byte[], only copying bytes for the specified length.
            Packet packet = default;
            packet.Create(netSendObject.Bytes, netSendObject.Length);

            // Only if a peer with this ID is found.
            if (Servers.TryGetValue(netSendObject.PeerParams.ID, out Peer peer))
            {
                // Verify that the peer is valid.
                if (peer.State != PeerState.Connected) return;

                // TEMP send on channel 0.
                peer.Send(0, ref packet);
            }
        }

        /// <summary>
        /// Queues messages to be sent to all remote hosts with IDs contained in the NetSendObject.
        /// </summary>
        /// <param name="netSendObject">  </param>
        internal void QueueMessageMany(NetSendObject netSendObject)
        {
            // Create packet from passed-in byte[], only copying bytes for the specified length.
            Packet packet = default;
            packet.Create(netSendObject.Bytes, netSendObject.Length);

            // Iterate over IDArray and try to message any valid Connection with the ID.
            foreach (var id in netSendObject.PeerParams.IDArray)
            {
                // Only if a peer with this ID is found.
                if (Servers.TryGetValue(id, out Peer peer))
                {
                    // Verify that the peer is valid.
                    if (peer.State != PeerState.Connected) return;

                    // TEMP send on channel 0.
                    peer.Send(0, ref packet);
                }
            }
        }

        /// <summary>
        /// Sends a packet to all connected peers.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant peer and payload data. </param>        
        internal void QueueMessageAll(NetSendObject netSendObject)
        {
            // Create packet from passed-in byte[], only copying bytes for the specified length.
            Packet packet = default;
            packet.Create(netSendObject.Bytes, netSendObject.Length);

            // Do not filter based on HostType because as a client, all remote hosts will be servers.
            foreach (var peer in Servers)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // TEMP send on channel 0.
                peer.Value.Send(0, ref packet);
            }
        }

        #endregion

        #region Receive Event Handlers

        private void HandleConnectEvent(ref Event connectEvent)
        {
            // If new connection is from another client, immediately disconnect that Peer.
            // OR if new connection is not in AllPeers (meaning this client did not initiate), disconnect.
            if (connectEvent.Peer.Port >= ClientPortMin || !AllPeers.ContainsKey(connectEvent.Peer.ID))
            {
                connectEvent.Peer.Disconnect(2000u);    // Data of 2000u indicates invalid new connection.
            }

            // Add this peer to Servers map.
            Servers[connectEvent.Peer.ID] = connectEvent.Peer;

            // Enqueue connect object with new peer's Connection for use by other threads.
            PeerParams peerParams = new(HostType.Server, connectEvent.Peer.ID, connectEvent.Peer.IP, connectEvent.Peer.Port);
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(
                peerParams, connectEvent.Data);
            netRecvQueue.Enqueue(dataObject);
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            // Remove Peer from map and enqueue if successful.
            if (Servers.Remove(disconnectEvent.Peer.ID, out Peer peer))
            {
                // Enqueue disconnect object with disconnected peer's data for use by other threads.
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(
                    peerParams, disconnectEvent.Data);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent)
        {
            // Remove Peer from map and enqueue if successful.
            if (Servers.Remove(timeoutEvent.Peer.ID, out Peer peer))
            {
                // Enqueue timeout object with timed-out peer's data for use by other threads.
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(
                    peerParams, timeoutEvent.Data);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleReceiveEvent(ref Event receiveEvent)
        {
            // Only if an entry for this peer exists in map.
            if (Servers.TryGetValue(receiveEvent.Peer.ID, out Peer peer))
            {
                // Copy packet payload into byte[].
                int length = receiveEvent.Packet.Length;
                byte[] bytes = new byte[length];
                receiveEvent.Packet.CopyTo(bytes);

                // Enqueue NetRecvObject with this peer's data.
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromMessage(peerParams, bytes, length);
                netRecvQueue.Enqueue(dataObject);
            }

            // Always dispose packet after handling receive, even if did not enqueue NetRecvObject.
            receiveEvent.Packet.Dispose();
        }

        #endregion

    }
}
