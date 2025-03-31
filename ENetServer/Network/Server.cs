using ENet;
using ENetServer.NetObjects;
using ENetServer.Network;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using static ENetServer.NetStatics;

namespace ENetServer.Network
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
        /// Map of all connected clients in form ID:Peer.
        /// </summary>
        private Dictionary<uint, Peer> Clients { get; } = new();
        private Dictionary<uint, Peer> Servers { get; } = new();
        private Dictionary<uint, Peer> AllPeers { get; } = new();

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
        internal void SetHostParameters(string ip, ushort port, int peerLimit, int channelLimit)
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

            // Disconnect all Clients and wait 3 seconds for ACKs.
            DisconnectAllOnStop(serverHost);

            // Finally, flush and dispose server host.
            serverHost.Flush();
            serverHost.Dispose();
        }

        /// <summary>
        /// Disconnects all Clients, then runs ENet service for 3 seconds to wait for ACKs (blocks).
        /// </summary>
        /// <param name="serverHost"> Non-null Host used only to silence warning. </param>
        private void DisconnectAllOnStop(Host serverHost)
        {
            // Disconnect all peers before disposing server (graceful disconnects).
            foreach (var peer in Clients)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // Disconnect with default data value.
                peer.Value.Disconnect(0);
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
                    case SendType.Connect_One:  // Will only be used to connect to other servers.
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
                    case SendType.Message_AllExcept:
                        {
                            QueueMessageAllExcept(netSendObject);
                            break;
                        }
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



        #region Connect/Disconnect Methods

        /// <summary>
        /// Queues connect request to be sent to one remote host next tick. Should only ever be used
        ///  to connect to other servers.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant remote host data. </param>
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
            foreach (var peer in Clients)
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
                Peer? pendingPeer = serverHost?.Connect(remoteAddress, 2, netSendObject.Data);
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
        /// Queues disconnect request for one remote host to be sent next tick.
        /// </summary>
        /// <param name="netSendObject">  </param>
        internal void QueueDisconnectOne(NetSendObject netSendObject)
        {
            // Only if a peer with this ID is found.
            uint id = netSendObject.PeerParams.ID;
            Peer peer;
            if (Clients.TryGetValue(id, out peer) || Servers.TryGetValue(id, out peer))
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
                Peer peer;
                if (Clients.TryGetValue(id, out peer) || Servers.TryGetValue(id, out peer))
                {
                    // Verify that the peer is valid.
                    if (peer.State != PeerState.Connected) return;

                    // Disconnect with data value.
                    peer.Disconnect(netSendObject.Data);
                }
            }
        }

        /// <summary>
        /// Queues disconnect requests to be sent to all connected remote hosts matching
        ///  the HostType contained in the NetSendObject.
        /// </summary>
        /// <param name="netSendObject">  </param>
        internal void QueueDisconnectAll(NetSendObject netSendObject)
        {
            HostType hostType = netSendObject.PeerParams.HostType;

            // If HostType is both, do not do any filtering.
            if (hostType == HostType.Both)
            {
                foreach (var peer in Clients)
                {
                    // Verify that the peer is valid.
                    if (peer.Value.State != PeerState.Connected) continue;

                    // Disconnect with data value.
                    peer.Value.Disconnect(netSendObject.Data);
                }
                foreach (var peer in Servers)
                {
                    if (peer.Value.State != PeerState.Connected) continue;
                    peer.Value.Disconnect(netSendObject.Data);
                }
            }
            // Else if HostType is exclusively one or the other, filter each Peer.
            else if (hostType == HostType.Server || hostType == HostType.Client)
            {
                bool isServer = (netSendObject.PeerParams.HostType != HostType.Server);
                Dictionary<uint, Peer> dict = (isServer) ? Servers : Clients;

                foreach (var peer in dict)
                {
                    if (peer.Value.State != PeerState.Connected) continue;

                    peer.Value.Disconnect(netSendObject.Data);
                }
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
            uint id = netSendObject.PeerParams.ID;
            Peer peer;
            if (Clients.TryGetValue(id, out peer) || Servers.TryGetValue(id, out peer))
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
                Peer peer;
                if (Clients.TryGetValue(id, out peer) || Servers.TryGetValue(id, out peer))
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

            HostType hostType = netSendObject.PeerParams.HostType;

            // If HostType is both, do not do any filtering.
            if (hostType == HostType.Both)
            {
                foreach (var peer in Clients)
                {
                    // Verify that the peer is valid.
                    if (peer.Value.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Value.Send(0, ref packet);
                }
                foreach (var peer in Servers)
                {
                    if (peer.Value.State != PeerState.Connected) continue;
                    peer.Value.Send(0, ref packet);
                }
            }
            // Else if HostType is exclusively one or the other, filter each Connection.
            else if (hostType == HostType.Server || hostType == HostType.Client)
            {
                bool isServer = (netSendObject.PeerParams.HostType != HostType.Server);
                Dictionary<uint, Peer> dict = (isServer) ? Servers : Clients;

                foreach (var peer in dict)
                {
                    if (peer.Value.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Value.Send(0, ref packet);
                }
            }
        }

        /// <summary>
        /// Sends a packet to all connected peers except one.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant peer and payload data. </param>        
        internal void QueueMessageAllExcept(NetSendObject netSendObject)
        {
            // Create packet from passed-in byte[], only copying bytes for the specified length.
            Packet packet = default;
            packet.Create(netSendObject.Bytes, netSendObject.Length);

            HostType hostType = netSendObject.PeerParams.HostType;

            // If HostType is both, do not do any filtering.
            if (hostType == HostType.Both)
            {
                foreach (var peer in Clients)
                {
                    // Skipped passed-in peer ID.
                    if (peer.Key == netSendObject.PeerParams.ID) continue;

                    // Verify that the peer is valid.
                    if (peer.Value.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Value.Send(0, ref packet);
                }
                foreach (var peer in Servers)
                {
                    if (peer.Key == netSendObject.PeerParams.ID) continue;
                    if (peer.Value.State != PeerState.Connected) continue;
                    peer.Value.Send(0, ref packet);
                }
            }
            // Else if HostType is exclusively one or the other, filter each Connection.
            else if (hostType == HostType.Server || hostType == HostType.Client)
            {
                bool isServer = (netSendObject.PeerParams.HostType != HostType.Server);
                Dictionary<uint, Peer> dict = (isServer) ? Servers : Clients;

                foreach (var peer in dict)
                {
                    // Skip passed-in peer ID.
                    if (peer.Key == netSendObject.PeerParams.ID) continue;

                    if (peer.Value.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Value.Send(0, ref packet);
                }
            }
        }

        #endregion

        #region Receive Event Handlers

        private void HandleConnectEvent(ref Event connectEvent)
        {
            // If below client port minimum, is server (server min (7777) is always less than client min (8888)).
            bool isServer = connectEvent.Peer.Port < ClientPortMin;

            // Add new Peer to correct Dictionary and AllPeers.
            Peer peer = connectEvent.Peer;
            PeerParams peerParams;
            if (isServer)
            {
                peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                Servers[connectEvent.Peer.ID] = connectEvent.Peer;
            }
            else
            {
                peerParams = new(HostType.Client, peer.ID, peer.IP, peer.Port);
                Clients[connectEvent.Peer.ID] = connectEvent.Peer;
            }
            AllPeers[connectEvent.Peer.ID] = connectEvent.Peer;

            // Enqueue connect object with new peer's data for use by other threads.
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(
                peerParams, connectEvent.Data);
            netRecvQueue.Enqueue(dataObject);
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            // Remove Peer from map and enqueue if successful.
            Peer peer;
            if (Clients.Remove(disconnectEvent.Peer.ID, out peer))
            {
                // Enqueue disconnect object with disconnected peer's data for use by other threads.
                PeerParams peerParams = new(HostType.Client, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(
                    peerParams, disconnectEvent.Data);
                netRecvQueue.Enqueue(dataObject);
            }
            if (Servers.Remove(disconnectEvent.Peer.ID, out peer))
            {
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(
                    peerParams, disconnectEvent.Data);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent)
        {
            // Remove Peer from map and enqueue if successful.
            Peer peer;
            if (Clients.Remove(timeoutEvent.Peer.ID, out peer))
            {
                // Enqueue disconnect object with disconnected peer's data for use by other threads.
                PeerParams peerParams = new(HostType.Client, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(
                    peerParams, timeoutEvent.Data);
                netRecvQueue.Enqueue(dataObject);
            }
            if (Servers.Remove(timeoutEvent.Peer.ID, out peer))
            {
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(
                    peerParams, timeoutEvent.Data);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleReceiveEvent(ref Event receiveEvent)
        {
            // Try to get Peer from map, operating if successful.
            Peer peer;
            if (Clients.TryGetValue(receiveEvent.Peer.ID, out peer))
            {
                // Copy packet payload into byte[].
                int length = receiveEvent.Packet.Length;
                byte[] bytes = new byte[length];
                receiveEvent.Packet.CopyTo(bytes);

                // Enqueue NetRecvObject with this peer's data.
                PeerParams peerParams = new(HostType.Client, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromMessage(
                    peerParams, bytes, length);
                netRecvQueue.Enqueue(dataObject);
            }
            if (Servers.TryGetValue(receiveEvent.Peer.ID, out peer))
            {
                int length = receiveEvent.Packet.Length;
                byte[] bytes = new byte[length];
                receiveEvent.Packet.CopyTo(bytes);

                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromMessage(
                    peerParams, bytes, length);
                netRecvQueue.Enqueue(dataObject);
            }

            // Always dispose packet after handling receive, even if did not enqueue NetRecvObject.
            receiveEvent.Packet.Dispose();
        }

        #endregion

    }
}
