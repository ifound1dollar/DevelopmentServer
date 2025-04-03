﻿using ENet;
using ENetServer.NetObjects;
using ENetServer.NetObjects.DataObjects;
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
        private Dictionary<uint, Peer> AllConnected { get; } = new();
        private HashSet<Peer> InitiatedPeers { get; } = new();
        private HashSet<Peer> AwaitingPeers { get; } = new();

        private Peer? MasterServer { get; set; } = null;
        private HashSet<uint> Checksums { get; } = [ 7777u, 8888u ];
        private HashSet<string> LoginTokens { get; } = [ "0f8fad5bd9cb469fa16570867728950e", "1f8fad5bd9cb469fa16570867728950e"];

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

            // Immediately attempt to connect to MasterServer on server start.
            // TODO: IMPLEMENT MASTER SERVER CONNECTION HERE, WAITING 3 SECONDS FOR CONNECTION SUCCESS
            // OR, MAYBE DO NOT WAIT FOR CONNECTION HERE - CONNECTION ATTEMPT WILL BE FIRST ENET
            //  EVENT DISPATCHED ONCE LOOP STARTS
            // IF WAIT FOR CONNECTION HERE: remove master server connect check from connect receive
            //  method. ELSE IF NOT WAITING FOR CONNECTION, leave master server connect check.
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

                // Disconnect with uint 300u, sending peer-initiated disconnect on host shutdown.
                peer.Value.Disconnect(300u);    // Is received by peer.
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
            if (port < 7776 || port >= ClientPortMin)
            {
                Console.WriteLine("[ERROR] Specified port out of range for new Connect attempt. Valid range: {0}-{1}",
                    7776, ClientPortMin - 1);
                return;
            }

            // Verify not trying to connect to a Host already connected to.
            foreach (var peer in AllConnected)
            {
                if (peer.Value.IP == ip && peer.Value.Port == port)
                {
                    Console.WriteLine("[ERROR] Cannot connect to an existing Connection. Aborting.");
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
                    InitiatedPeers.Add(pendingPeer.Value);
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
            if (AllConnected.TryGetValue(netSendObject.PeerParams.ID, out Peer peer))
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
                if (AllConnected.TryGetValue(id, out Peer peer))
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
            // Use HostType to determine which map to iterate over.
            Dictionary<uint, Peer> dict;
            HostType hostType = netSendObject.PeerParams.HostType;
            switch (hostType)
            {
                case HostType.Both: dict = AllConnected; break;
                case HostType.Server: dict = Servers; break;
                case HostType.Client: dict = Clients; break;
                default: dict = []; break; // Empty if no HostType (send to none).
            }

            // Iterate over all Peers in map and disconnect if valid.
            foreach (var peer in dict)
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
            if (AllConnected.TryGetValue(netSendObject.PeerParams.ID, out Peer peer))
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
            foreach (uint id in netSendObject.PeerParams.IDArray)
            {
                // Only if a peer with this ID is found.
                if (AllConnected.TryGetValue(id, out Peer peer))
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

            // Use HostType to determine which map to iterate over.
            Dictionary<uint, Peer> dict;
            HostType hostType = netSendObject.PeerParams.HostType;
            switch (hostType)
            {
                case HostType.Both: dict = AllConnected; break;
                case HostType.Server: dict = Servers; break;
                case HostType.Client: dict = Clients; break;
                default: dict = []; break; // Empty if no HostType (send to none).
            }

            // Iterate over all elements in correct map, sending to each valid Peer.
            foreach (var peer in dict)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // TEMP send on channel 0.
                peer.Value.Send(0, ref packet);
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

            // Use HostType to determine which map to iterate over.
            Dictionary<uint, Peer> dict;
            HostType hostType = netSendObject.PeerParams.HostType;
            switch (hostType)
            {
                case HostType.Both: dict = AllConnected; break;
                case HostType.Server: dict = Servers; break;
                case HostType.Client: dict = Clients; break;
                default: dict = []; break; // Empty if no HostType (send to none).
            }

            // Iterate over all elements in correct map, sending to each valid Peer.
            foreach (var peer in dict)
            {
                // Skipped passed-in peer ID.
                if (peer.Key == netSendObject.PeerParams.ID) continue;

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
            Peer peer = connectEvent.Peer;

            if (peer.Port >= NetStatics.ClientPortMin)      // Client ports 8888+.
            {
                ProcessClientConnect(ref connectEvent);
            }
            else if (peer.Port >= NetStatics.ServerPortMin) // Server ports between 7777 and 8887.
            {
                ProcessGameServerConnect(ref connectEvent);
            }
            else if (peer.Port == 7776)                     // Master Server port should be 7776.
            {
                ProcessMasterServerConnect(ref connectEvent);
            }
            else
            {
                // DO NOTHING HERE? IS INVALID PORT RANGE (below 7776)
            }
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            Peer peer = disconnectEvent.Peer;

            if (peer.Port >= NetStatics.ClientPortMin)      // Client ports 8888+.
            {
                // Remove Peer from maps and enqueue if successful.
                if (Clients.Remove(peer.ID, out _) || AllConnected.Remove(peer.ID, out _))
                {
                    // If Data uint is 0, then event is an automatic ACK from disconnect initialized here.
                    uint data = (disconnectEvent.Data == 0) ? 201u : disconnectEvent.Data;

                    // Enqueue disconnect object with disconnected peer's data for use by other threads.
                    PeerParams peerParams = new(HostType.Client, peer.ID, peer.IP, peer.Port);
                    NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(
                        peerParams, data);
                    netRecvQueue.Enqueue(dataObject);
                }
            }
            else if (peer.Port >= NetStatics.ServerPortMin) // Server ports between 7777 and 8887.
            {
                // Remove Peer from maps and enqueue if successful.
                if (Servers.Remove(peer.ID, out _) || AllConnected.Remove(peer.ID, out _))
                {
                    // If Data uint is 0, then event is an automatic ACK from disconnect initialized here.
                    uint data = (disconnectEvent.Data == 0) ? 201u : disconnectEvent.Data;

                    // Enqueue disconnect object with disconnected peer's data for use by other threads.
                    PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                    NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(
                        peerParams, data);
                    netRecvQueue.Enqueue(dataObject);
                }
            }
            else if (peer.Port == 7776)                     // Master Server port should be 7776.
            {
                // DOES THIS MATTER TO GAME THREAD?
            }
            else
            {
                // DO NOTHING HERE? IS INVALID PORT RANGE (below 7776)
            }
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent)
        {
            Peer peer = timeoutEvent.Peer;

            if (peer.Port >= NetStatics.ClientPortMin)      // Client ports 8888+.
            {
                // Remove Peer from maps and enqueue if successful.
                if (Clients.Remove(peer.ID, out _) || AllConnected.Remove(peer.ID, out _))
                {
                    // Enqueue disconnect object with disconnected peer's data for use by other threads.
                    PeerParams peerParams = new(HostType.Client, peer.ID, peer.IP, peer.Port);
                    NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(
                        peerParams, 400u);
                    netRecvQueue.Enqueue(dataObject);
                }
            }
            else if (peer.Port >= NetStatics.ServerPortMin) // Server ports between 7777 and 8887.
            {
                // Remove Peer from maps and enqueue if successful.
                if (Servers.Remove(peer.ID, out _) || AllConnected.Remove(peer.ID, out _))
                {
                    // Enqueue disconnect object with disconnected peer's data for use by other threads.
                    PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                    NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(
                        peerParams, 400u);
                    netRecvQueue.Enqueue(dataObject);
                }
            }
            else if (peer.Port == 7776)                     // Master Server port should be 7776.
            {
                // DOES THIS MATTER TO GAME THREAD?
            }
            else
            {
                // DO NOTHING HERE? IS INVALID PORT RANGE (below 7776)
            }
        }

        private void HandleReceiveEvent(ref Event receiveEvent)
        {
            Peer peer = receiveEvent.Peer;

            if (peer.Port >= NetStatics.ClientPortMin)      // Client ports 8888+.
            {
                // If received from client which is not in map, is not yet validated.
                if (!Clients.ContainsKey(peer.ID))
                {
                    ProcessClientTokenVerification(ref receiveEvent);
                }
                // Else is in map (validated), so enqueue as normal message.
                else
                {
                    ProcessClientMessage(ref receiveEvent);
                }
            }
            else if (peer.Port >= NetStatics.ServerPortMin) // Server ports between 7777 and 8887.
            {
                // If received from server which is not in map, is not yet validated.
                if (!Servers.ContainsKey(peer.ID))
                {
                    ProcessGameServerTokenVerification(ref receiveEvent);
                }
                // Else is in map (validated), so enqueue as normal message.
                else
                {
                    ProcessGameServerMessage(ref receiveEvent);
                }
            }
            else if (peer.Port == 7776)                     // Master Server port should be 7776.
            {
                ProcessMasterServerMessage(ref receiveEvent);
            }
            else
            {
                // DO NOTHING HERE? IS INVALID PORT RANGE (below 7776)
            }

            // Always dispose packet after handling receive, even if did not enqueue NetRecvObject.
            receiveEvent.Packet.Dispose();
        }

        #endregion

        #region Connect processing

        private void ProcessMasterServerConnect(ref Event connectEvent)
        {
            Peer peer = connectEvent.Peer;
            if (MasterServer == null)
            {
                Console.WriteLine("[CONNECT] Successfully connected to master server (ID: {0}), Address {1}:{2}",
                peer.ID, peer.IP, peer.Port);

                // Set MasterServer Peer.
                MasterServer = connectEvent.Peer;
            }
            else
            {
                Console.WriteLine("[ERROR] Invalid new connection on port 7776 - MasterServer connection already exists.");

                // Data uint of 1006 indicates master server validation error.
                peer.DisconnectNow(1006u);
            }
        }

        private void ProcessGameServerConnect(ref Event connectEvent)
        {
            // If Peer is in InitiatedPeers, then this server was the initiator of the attempted
            //  connection (added to InitiatedPeers on connection request send).
            // Else, this server is receiving an initial connection attempt (totally new Peer).
            Peer peer = connectEvent.Peer;
            bool isInitiator = InitiatedPeers.Contains(peer);

            if (isInitiator)
            {
                // Add to InitiatedPeers, which contains Peers with prelim connections which we initiated.
                InitiatedPeers.Add(peer);

                // Initial connection successful, so must send over login token immediately.
                string token = "1f8fad5bd9cb469fa16570867728950e";
                token = NetStatics.FormatStringForSend(token);

                // Create packet with login token and send.
                Packet packet = default;
                packet.Create(NetStatics.GetBytes(token));
                peer.Send(0, ref packet);
            }
            else
            {
                // If new connection passes valid checksum, make preliminary connection.
                if (/*Checksums.Remove(connectEvent.Data)*/Checksums.Contains(connectEvent.Data))
                {
                    // Add new Peer to AwaitingPeers, awaiting login token.
                    AwaitingPeers.Add(peer);
                }
                // Else force disconnect immediately.
                else
                {
                    Console.WriteLine("[ERROR] Invalid new connection from {0}:{1} - checksum failed. Checksum: {2}",
                        peer.IP, peer.Port, connectEvent.Data);

                    // Data uint of 1001u indicates server validation error.
                    peer.DisconnectNow(1001u);
                }
                
            }
        }

        private void ProcessClientConnect(ref Event connectEvent)
        {
            Peer peer = connectEvent.Peer;

            // If new connection passes valid checksum, make preliminary connection.
            if (/*Checksums.Remove(connectEvent.Data)*/Checksums.Contains(connectEvent.Data))
            {
                // Add new client to AwaitingPeers, as we are awaiting login token from this client Peer.
                AwaitingPeers.Add(peer);
            }
            // Else force disconnect immediately.
            else
            {
                Console.WriteLine("[ERROR] Invalid new connection from {0}:{1} - checksum failed. Checksum: {2}",
                    peer.IP, peer.Port, connectEvent.Data);

                // Data uint of 1000u indicates client validation error.
                peer.DisconnectNow(1000u);
            }
        }

        #endregion

        #region Message processing

        private void ProcessMasterServerMessage(ref Event receiveEvent)
        {
            // READ DATA IN EXPECTED STRUCTURE AND ADD TO BOTH HASH SETS
        }

        private void ProcessGameServerMessage(ref Event receiveEvent)
        {
            Peer peer = receiveEvent.Peer;

            // Copy packet payload into byte[].
            int length = receiveEvent.Packet.Length;
            byte[] bytes = new byte[length];
            receiveEvent.Packet.CopyTo(bytes);

            // Enqueue NetRecvObject with this peer's data.
            PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromMessage(
                peerParams, bytes, length);
            netRecvQueue.Enqueue(dataObject);
        }

        private void ProcessGameServerTokenVerification(ref Event receiveEvent)
        {
            // NOTE: This method is the first to be called in a server->server connection attempt,
            //  specifically when the connected-to server receives a message from a server Peer
            //  which is not yet in the Servers map.
            // The initiating client will have sent a connect request, received a connect response,
            //  then sent over its login ack with validation being handled here.
            // This method processes an unvalidated message from another server. It will either
            //  send a 'verification success' ACK back to the initiating server, or will
            //  interpret this message as the ACK (depending on whether this Peer is pending or
            //  not).
            // If is pending, then this is the initiating server that is waiting for the ACK, so
            //  check whether this message is in correct ACK format.
            // Else this is the receiving server, so actually verify the login ACK to fully
            //  validate the other server. Sends back a validation ACK if successful token.

            Peer peer = receiveEvent.Peer;

            // Copy packet payload into byte[].
            int length = receiveEvent.Packet.Length;
            byte[] bytes = new byte[length];
            receiveEvent.Packet.CopyTo(bytes);

            // Get EITHER login ack OR validation ACK from packet.
            string str = NetStatics.GetString(bytes, 0, length);
            str = NetStatics.FormatStringFromReceive(str);

            // If we are initiator, this first message should be validation ACK.
            if (InitiatedPeers.Remove(peer))
            {
                // If validation response contains expected ACK data, add Peer to maps (now valid)
                if (!string.IsNullOrEmpty(str)) // TEMP NOT ACTUAL VALIDATION
                {
                    Servers[peer.ID] = peer;
                    AllConnected[peer.ID] = peer;

                    // Validation has fully completed, so enqueue successful connect object.
                    PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                    NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(
                        peerParams, 101u);
                    netRecvQueue.Enqueue(dataObject);
                }
                else
                {
                    Console.WriteLine("[ERROR] Error in validation ACK, disconnecting (ID: {0})", peer.ID);

                    // Immediately disconnect this server, data uint 1001u means validation error.
                    peer.DisconnectNow(1001u);
                    return;
                }
            }
            // Else is not initiator and is awaiting login token, so should verify login token.
            else if (AwaitingPeers.Remove(peer))
            {
                if (/*LoginTokens.Remove(textDataObject.String*/LoginTokens.Contains(str))
                {
                    // If valid login token, this Peer is now fully connected, so add to maps.
                    Servers[peer.ID] = peer;
                    AllConnected[peer.ID] = peer;

                    // Create packet with validation ACK and send.
                    string ack = NetStatics.FormatStringForSend("Server validation successful.");
                    Packet packet = default;
                    packet.Create(NetStatics.GetBytes(ack));
                    peer.Send(0, ref packet);

                    // Validation has fully completed, so enqueue successful connect object.
                    PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                    NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(
                        peerParams, 100u);
                    netRecvQueue.Enqueue(dataObject);
                }
                else
                {
                    Console.WriteLine("[ERROR] Server failed validation check, disconnecting (ID: {0})",
                        peer.ID);

                    // Immediately disconnect this server, data uint 1001u means validation error.
                    peer.DisconnectNow(1001u);
                    return;
                }
            }
        }

        private void ProcessClientMessage(ref Event receiveEvent)
        {
            Peer peer = receiveEvent.Peer;

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

        private void ProcessClientTokenVerification(ref Event receiveEvent)
        {
            Peer peer = receiveEvent.Peer;

            // Copy packet payload into byte[].
            int length = receiveEvent.Packet.Length;
            byte[] bytes = new byte[length];
            receiveEvent.Packet.CopyTo(bytes);

            // Get login token from raw packet data, then check whether is a valid token.
            string str = NetStatics.GetString(bytes, 0, length);
            str = NetStatics.FormatStringFromReceive(str);

            // Remove this Peer from awaiting HashSet and validate connection if successful.
            if (AwaitingPeers.Remove(peer))
            {
                if (/*LoginTokens.Remove(textDataObject.String*/LoginTokens.Contains(str))
                {
                    // If valid login token, this Peer is now fully connected so add to maps.
                    Clients[peer.ID] = peer;
                    AllConnected[peer.ID] = peer;

                    // Create packet with validation ACK and send.
                    string ack = NetStatics.FormatStringForSend("Server validation successful.");
                    Packet packet = default;
                    packet.Create(NetStatics.GetBytes(ack));
                    peer.Send(0, ref packet);

                    // Enqueue successful connect and pass to main/game thread.
                    PeerParams peerParams = new(HostType.Client, peer.ID, peer.IP, peer.Port);
                    NetRecvObject netRecvObject = NetRecvObject.Factory.CreateFromConnect(peerParams, 100u);
                    netRecvQueue.Enqueue(netRecvObject);
                }
                else
                {
                    Console.WriteLine("[DISCONNECT] Client failed validation check, disconnecting (ID: {0})",
                        peer.ID);

                    // Immediately disconnect this client, data uint 1000u means validation error.
                    peer.DisconnectNow(1000u);
                }
            }
        }

        #endregion
    }
}
