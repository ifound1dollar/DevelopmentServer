using ENet;
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



        private Dictionary<uint, PeerData> Clients { get; } = new();
        private Dictionary<uint, PeerData> Servers { get; } = new();
        private Dictionary<uint, PeerData> AllConnected { get; } = new();
        private Dictionary<string, PeerData> PendingPeers { get; } = new();

        private Peer? MasterServer { get; set; } = null;
        private Dictionary<string, BlacklistData> BlacklistMap { get; } = new();
        private Dictionary<string, ValidationData> ValidationMap { get; } = new();

        private Dictionary<string, string> OutgoingTokens { get; } = new(); // for login tokens we send

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

            // TODO: IMPLEMENT MASTER SERVER CONNECTION HERE, WAITING 3 SECONDS FOR CONNECTION SUCCESS
            // OR, MAYBE DO NOT WAIT FOR CONNECTION HERE - CONNECTION ATTEMPT WILL BE FIRST ENET
            //  EVENT DISPATCHED ONCE LOOP STARTS
            // IF WAIT FOR CONNECTION HERE: remove master server connect check from connect receive
            //  method. ELSE IF NOT WAITING FOR CONNECTION, keep master server connect check.

            // TODO: REMOVE TEMPORARY POPULATE VALIDATION MAP WITH DATA
            ValidationMap["127.0.0.1:8888"] = new ValidationData("127.0.0.1", 8888, "0f8fad5bd9cb469fa16570867728950e");
            ValidationMap["127.0.0.1:8889"] = new ValidationData("127.0.0.1", 8889, "0f8fad5bd9cb469fa16570867728950e");
            ValidationMap["127.0.0.1:8890"] = new ValidationData("127.0.0.1", 8890, "0f8fad5bd9cb469fa16570867728950e");

            ValidationMap["127.0.0.1:7777"] = new ValidationData("127.0.0.1", 7777, "1f8fad5bd9cb469fa16570867728950e");
            ValidationMap["127.0.0.1:7778"] = new ValidationData("127.0.0.1", 7778, "1f8fad5bd9cb469fa16570867728950e");
            ValidationMap["127.0.0.1:7779"] = new ValidationData("127.0.0.1", 7779, "1f8fad5bd9cb469fa16570867728950e");

            // TODO: REMOVE TEMPORARY LOGIN TOKEN ADDS
            OutgoingTokens["127.0.0.1:7777"] = "1f8fad5bd9cb469fa16570867728950e";
            OutgoingTokens["127.0.0.1:7778"] = "1f8fad5bd9cb469fa16570867728950e";
            OutgoingTokens["127.0.0.1:7779"] = "1f8fad5bd9cb469fa16570867728950e";
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
            foreach (var peerData in Clients)
            {
                // Verify that the peer is valid.
                if (peerData.Value.Peer.State != PeerState.Connected) continue;

                // Disconnect with uint 300u, sending peer-initiated disconnect on host shutdown.
                peerData.Value.Peer.Disconnect(300u);   // Is received by peer.
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
            if (port < ServerPortMin || port >= ClientPortMin)
            {
                Console.WriteLine("[ERROR] Specified port out of range for new Connect attempt. Valid range: {0}-{1}",
                    ServerPortMin, ClientPortMin - 1);
                return;
            }

            // Verify not trying to connect to a Host already connected to.
            foreach (var peerData in AllConnected)
            {
                if (peerData.Value.Peer.IP == ip && peerData.Value.Peer.Port == port)
                {
                    Console.WriteLine("[ERROR] Cannot connect to an existing Connection. Aborting.");
                    return;
                }
            }

            // Create Address object with IP and Port from NetSendObject.
            Address remoteAddress = new();
            remoteAddress.SetIP(ip);
            remoteAddress.Port = port;

            try
            {
                // Get checksum from login token before connect attempt (get value, do not remove).
                string key = NetStatics.GetAddressString(ip, port);
                if (!OutgoingTokens.TryGetValue(key, out string? token)) return;
                uint checksum = NetStatics.CalculateChecksum(token);

                // Actually make connection request, which returns null or throws an exception on failure.
                Peer? pendingPeer = serverHost?.Connect(remoteAddress, 2, checksum);
                if (pendingPeer != null)
                {
                    pendingPeer.Value.Timeout(32, 5000, 10000); //32 and 5000 are default, last param default is 30000 (30s)
                    
                    // Add this peer to pending peers.
                    PeerData peerData = new((Peer)pendingPeer, PeerData.CustomState.Initiated);
                    PendingPeers[key] = peerData;
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
            if (AllConnected.TryGetValue(netSendObject.PeerParams.ID, out PeerData? peerData))
            {
                // Verify that the peer is valid (using ENet PeerState here).
                if (peerData.Peer.State != PeerState.Connected) return;

                // Disconnect with data value.
                peerData.Peer.Disconnect(netSendObject.Data);
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
                if (AllConnected.TryGetValue(id, out PeerData? peerData))
                {
                    // Verify that the peer is valid (using ENet PeerState here).
                    if (peerData.Peer.State != PeerState.Connected) return;

                    // Disconnect with data value.
                    peerData.Peer.Disconnect(netSendObject.Data);
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
            Dictionary<uint, PeerData> dict;
            HostType hostType = netSendObject.PeerParams.HostType;
            switch (hostType)
            {
                case HostType.Both: dict = AllConnected; break;
                case HostType.Server: dict = Servers; break;
                case HostType.Client: dict = Clients; break;
                default: dict = []; break; // Empty if no HostType (send to none).
            }

            // Iterate over all Peers in map and disconnect if valid.
            foreach (var peerData in dict)
            {
                // Verify that the peer is valid (using ENet PeerState here).
                if (peerData.Value.Peer.State != PeerState.Connected) continue;

                // Disconnect with data value.
                peerData.Value.Peer.Disconnect(netSendObject.Data);
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
            if (AllConnected.TryGetValue(netSendObject.PeerParams.ID, out PeerData? peerData))
            {
                // Verify that the peer is valid (using ENet PeerState here).
                if (peerData.Peer.State != PeerState.Connected) return;

                // TEMP send on channel 0.
                peerData.Peer.Send(0, ref packet);
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
                if (AllConnected.TryGetValue(id, out PeerData? peerData))
                {
                    // Verify that the peer is valid (using ENet PeerState here).
                    if (peerData.Peer.State != PeerState.Connected) return;

                    // TEMP send on channel 0.
                    peerData.Peer.Send(0, ref packet);
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
            Dictionary<uint, PeerData> dict;
            HostType hostType = netSendObject.PeerParams.HostType;
            switch (hostType)
            {
                case HostType.Both: dict = AllConnected; break;
                case HostType.Server: dict = Servers; break;
                case HostType.Client: dict = Clients; break;
                default: dict = []; break; // Empty if no HostType (send to none).
            }

            // Iterate over all elements in correct map, sending to each valid Peer.
            foreach (var peerData in dict)
            {
                // Verify that the peer is valid (using ENet PeerState here).
                if (peerData.Value.Peer.State != PeerState.Connected) continue;

                // TEMP send on channel 0.
                peerData.Value.Peer.Send(0, ref packet);
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
            Dictionary<uint, PeerData> dict;
            HostType hostType = netSendObject.PeerParams.HostType;
            switch (hostType)
            {
                case HostType.Both: dict = AllConnected; break;
                case HostType.Server: dict = Servers; break;
                case HostType.Client: dict = Clients; break;
                default: dict = []; break; // Empty if no HostType (send to none).
            }

            // Iterate over all elements in correct map, sending to each valid Peer.
            foreach (var peerData in dict)
            {
                // Skipped passed-in peer ID.
                if (peerData.Value.Peer.ID == netSendObject.PeerParams.ID) continue;

                // Verify that the peer is valid.
                if (peerData.Value.Peer.State != PeerState.Connected) continue;

                // TEMP send on channel 0.
                peerData.Value.Peer.Send(0, ref packet);
            }
        }

        #endregion

        #region Receive Event Handlers

        private void HandleConnectEvent(ref Event connectEvent)
        {
            Peer peer = connectEvent.Peer;
            string key = NetStatics.GetAddressString(peer.IP, peer.Port);

            // FIRST, verify that new connection is not currently blacklisted.
            if (BlacklistMap.TryGetValue(key, out BlacklistData? blacklistData))
            {
                // If connected client is still blacklisted, immediately disconnect.
                if (blacklistData.IsCurrentlyBlacklisted())
                {
                    DisconnectOnBlacklist(ref peer);

                    return;
                }
            }

            // Else, handle new connection.
            if (peer.Port >= NetStatics.ClientPortMin)      // Client ports 8888+.
            {
                ProcessIncomingConnection(ref peer, key, connectEvent.Data, isServer: false);
            }
            else if (peer.Port >= NetStatics.ServerPortMin) // Server ports between 7777 and 8887.
            {
                ProcessIncomingConnection(ref peer, key, connectEvent.Data, isServer: true);
            }
            else if (peer.Port == 7776)                     // Master Server port should be 7776.
            {
                ProcessMasterServerConnect(ref peer, key, connectEvent.Data);
            }
            else
            {
                // DO NOTHING HERE? IS INVALID PORT RANGE (below 7776)
            }
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            Peer peer = disconnectEvent.Peer;
            string key = NetStatics.GetAddressString(peer.IP, peer.Port);

            // If was in PendingPeers, then note failure to connect.
            if (PendingPeers.Remove(key))
            {
                Console.WriteLine("[ERROR] Failed to connect to server at address {0}:{1}, Code: {2}",
                    peer.IP, peer.Port, disconnectEvent.Data);
            }

            if (peer.Port >= NetStatics.ClientPortMin)      // Client ports 8888+.
            {
                // Remove Peer from maps and enqueue if successful.
                if (Clients.Remove(peer.ID) || AllConnected.Remove(peer.ID))
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
                if (Servers.Remove(peer.ID) || AllConnected.Remove(peer.ID))
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
                if (Clients.Remove(peer.ID) || AllConnected.Remove(peer.ID))
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
                if (Servers.Remove(peer.ID) || AllConnected.Remove(peer.ID))
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

            // Copy packet payload into byte[].
            int length = receiveEvent.Packet.Length;
            byte[] bytes = new byte[length];
            receiveEvent.Packet.CopyTo(bytes);

            if (peer.Port >= NetStatics.ClientPortMin)      // Client ports 8888+.
            {
                if (!Clients.ContainsKey(peer.ID))
                {
                    // If received from client which is not in map, is not yet validated.
                    ProcessValidation(ref peer, bytes, length, isServer: false);
                }
                else
                {
                    // Else is in map (validated), so enqueue as normal message.
                    ProcessRegularMessage(ref peer, bytes, length, isServer: false);
                }
            }
            else if (peer.Port >= NetStatics.ServerPortMin) // Server ports between 7777 and 8887.
            {
                if (!Servers.ContainsKey(peer.ID))
                {
                    // If received from server which is not in map, is not yet validated.
                    ProcessValidation(ref peer, bytes, length, isServer: true);
                }
                else
                {
                    // Else is in map (validated), so enqueue as normal message.
                    ProcessRegularMessage(ref peer, bytes, length, isServer: true);
                }
            }
            else if (peer.Port == 7776)                     // Master Server port should be 7776.
            {
                ProcessMasterServerMessage(ref peer, bytes, length);
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

        private void ProcessMasterServerConnect(ref Peer peer, string key, uint inChecksum)
        {
            if (MasterServer == null)
            {
                Console.WriteLine("[CONNECT] Successfully connected to master server (ID: {0}), Address {1}:{2}",
                peer.ID, peer.IP, peer.Port);

                // Set MasterServer Peer.
                MasterServer = peer;
            }
            else
            {
                Console.WriteLine("[ERROR] Invalid new connection on port 7776 - MasterServer connection already exists.");

                // Data uint of 1500u indicates master server connection error.
                peer.DisconnectNow(1500u);
            }
        }

        private void ProcessIncomingConnection(ref Peer peer, string key, uint inChecksum, bool isServer)
        {
            // If this Peer not in PendingPeers, then is totally new connection so compare checksum.
            if (!PendingPeers.TryGetValue(key, out PeerData? peerData))
            {
                // If new connection passes valid checksum, make preliminary connection.
                if (ValidationMap.TryGetValue(key, out ValidationData? validationData))
                {
                    if (validationData.CompareChecksum(inChecksum))
                    {
                        // Add new Peer to PendingPeers with AwaitingToken state.
                        PeerData data = new(peer, PeerData.CustomState.AwaitingToken);
                        PendingPeers.Add(key, data);

                        return;
                    }
                }

                // If either of the above branches fail, blacklist and force disconnect immediately.
                BlacklistPeer(ref peer, key);
                DisconnectOnChecksumFail(ref peer, isServer: isServer);

                return;
            }

            // Else is in PendingPeers, and if Initiated by this server, immediately send full login token.
            if (peerData.State == PeerData.CustomState.Initiated)
            {
                // Initial connection successful, so must send over login token immediately.
                if (!OutgoingTokens.TryGetValue(key, out string? token)) return;
                //if (!OutgoingTokens.Remove(key, out string? token)) return;
                token = NetStatics.FormatStringForSend(token);

                // Create packet with login token and send.
                Packet packet = default;
                packet.Create(NetStatics.GetBytes(token));
                peer.Send(0, ref packet);
            }

            // A new connection that exists in PendingPeers WITHOUT Initiated state should never happen.
        }

        #endregion

        #region Message processing

        private void ProcessMasterServerMessage(ref Peer peer, byte[] bytes, int length)
        {
            // READ DATA IN EXPECTED STRUCTURE AND ADD TO BOTH HASH SETS
        }

        private void ProcessRegularMessage(ref Peer peer, byte[] bytes, int length, bool isServer)
        {
            HostType hostType = (isServer) ? HostType.Server : HostType.Client;

            // Enqueue NetRecvObject with this peer's data.
            PeerParams peerParams = new(hostType, peer.ID, peer.IP, peer.Port);
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromMessage(
                peerParams, bytes, length);
            netRecvQueue.Enqueue(dataObject);
        }

        #endregion

        #region Validation processing

        private void ProcessValidation(ref Peer peer, byte[] bytes, int length, bool isServer)
        {
            string key = NetStatics.GetAddressString(peer.IP, peer.Port);

            // If Peer does not exist in PendingPeers, then received message from unknown connection.
            if (!PendingPeers.TryGetValue(key, out PeerData? peerData))
            {
                BlacklistPeer(ref peer, key);
                DisconnectOnUnknownConnection(ref peer);
                return;
            }

            // Get raw string payload from packet, which could be an ACK or a login token.
            string str = NetStatics.GetString(bytes, 0, length);
            str = NetStatics.FormatStringFromReceive(str);

            // If we are initiator, this first message should be validation ACK.
            if (peerData.State == PeerData.CustomState.Initiated)           // Outgoing only
            {
                ProcessAckAsInitiator(peerData, key, responseString: str, isServer: isServer);
            }
            // Else if not initiator and is awaiting login token, so should verify login token.
            else if (peerData.State == PeerData.CustomState.AwaitingToken)  // Incoming only
            {
                ProcessTokenAsAwaiting(peerData, key, tokenString: str, isServer: isServer);
            }
            // Else if pending Peer is validated but has not yet returned ACK (must return our ACK).
            else if (peerData.State == PeerData.CustomState.AwaitingAck)    // Incoming only
            {
                ProcessFinalAckResponse(peerData, key, ackString: str, isServer: isServer);
            }
        }

        private void ProcessAckAsInitiator(PeerData peerData, string key, string responseString, bool isServer)
        {
            // NOTE: This method will only ever be called when processing a connection we initiated.
            // As initiator (having sent connection request and received successful response), we
            //  automatically sent our login token immediately after receiving the successful
            //  connect response.
            // Our first message received from the server, if login token validation was successful,
            //  will be a validation ACK noting that it was successful. We must verify that this
            //  ACK is correct, and if so, we are considered fully connected to the remote server.
            // We must send a response ACK which acknowledges that we received the validation ACK
            //  to fully complete the connection process. The server must know that we received
            //  the validation ACK and are ready to receive data.

            Dictionary<uint, PeerData> connectionDict = (isServer) ? Servers : Clients;
            HostType hostType = (isServer) ? HostType.Server : HostType.Client;
            Peer peer = peerData.Peer;

            // If validation response contains expected ACK data, add Peer to maps (now valid)
            if (responseString.Equals("Login token validation successful."))    // TODO: COMPARE ACTUAL VALIDATION ACK MESSAGE
            {
                // Add Peer to fully-connected maps, removing from PendingPeers and setting State.
                PendingPeers.Remove(key);
                peerData.State = PeerData.CustomState.Connected;
                connectionDict[peer.ID] = peerData;
                AllConnected[peer.ID] = peerData;

                // Create packet with response ACK and send.
                string ack = NetStatics.FormatStringForSend("Validation ACK received successfully.");
                Packet packet = default;
                packet.Create(NetStatics.GetBytes(ack));
                peerData.Peer.Send(0, ref packet);

                // Validation has fully completed, so enqueue successful connect object.
                PeerParams peerParams = new(hostType, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(
                    peerParams, 101u);
                netRecvQueue.Enqueue(dataObject);

                return;
            }

            // Disconnect on validation ACK failure, but DO NOT blacklist (we are the initiator).
            DisconnectOnAckFail(ref peer, isServer);
        }

        private void ProcessTokenAsAwaiting(PeerData peerData, string key, string tokenString, bool isServer)
        {
            // NOTE: This method will only ever be called when processing an incoming connection.
            // When a Peer is awaiting, it has completed an initial connection with a successful
            //  checksum validation, but we are awaiting the full login token to be sent by this
            //  awaiting Peer.
            // The Peer's first message must be raw login token data, which will be compared
            //  against the awaiting ValidationData (if it exists).
            // If the login token validation is successful, we must send a validation ACK to the
            //  Peer to notify it that it passed validation and should consider itself fully
            //  connected.
            // NOTE: We must await an response ACK from the Peer acknowledging that they received
            //  the validation ACK before we fully consider the connection fully completed on our
            //  end.

            Peer peer = peerData.Peer;

            // If new connection contains valid login token, complete connection.
            if (ValidationMap.TryGetValue(key, out ValidationData? validationData))
            {
                if (validationData.CompareLoginToken(tokenString))
                {
                    // Update PeerData object's state to PendingAck (from PendingToken).
                    peerData.State = PeerData.CustomState.AwaitingAck;

                    // Create packet with validation ACK and send.
                    string ack = NetStatics.FormatStringForSend("Login token validation successful.");
                    Packet packet = default;
                    packet.Create(NetStatics.GetBytes(ack));
                    peer.Send(0, ref packet);

                    // Remove Peer from Blacklist and Validation maps now that connection is made.
                    BlacklistMap.Remove(key);
                    //ValidationMap.Remove(key);

                    return;
                }
            }

            // If either of the above branches fail, blacklist and force disconnect immediately.
            BlacklistPeer(ref peer, key);
            DisconnectOnLoginTokenFail(ref peer, isServer);
        }

        private void ProcessFinalAckResponse(PeerData peerData, string key, string ackString, bool isServer)
        {
            // NOTE: This method will only ever be called when processing an incoming connection.
            // Remote host must return an ACK which acknowledges that it received our original
            //  post-token-validation ACK. Only once this final ACK is received, we consider
            //  the connection 'official', adding the Peer to fully-connected maps. And
            //  enqueuing a successful connect object.

            Dictionary<uint, PeerData> connectionDict = (isServer) ? Servers : Clients;
            HostType hostType = (isServer) ? HostType.Server : HostType.Client;
            Peer peer = peerData.Peer;

            // If return ACK response contains expected ACK data, add Peer to maps (now valid)
            if (ackString.Equals("Validation ACK received successfully."))  // TODO: COMPARE ACTUAL RETURN ACK MESSAGE
            {
                // Add Peer to fully-connected maps, removing from PendingPeers and setting State.
                PendingPeers.Remove(key);
                peerData.State = PeerData.CustomState.Connected;
                connectionDict[peer.ID] = peerData;
                AllConnected[peer.ID] = peerData;

                // Validation has fully completed, so enqueue successful connect object.
                PeerParams peerParams = new(hostType, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(
                    peerParams, 100u);
                netRecvQueue.Enqueue(dataObject);

                return;
            }
            
            // Disconnect on returned ACK failure, but do not blacklist (did not fail validation).
            DisconnectOnAckFail(ref peer, isServer);
        }

        #endregion

        private static void DisconnectOnBlacklist(ref Peer peer)
        {
            Console.WriteLine("[ERROR] Rejecting new connection from {0}:{1}, Reason: Blacklisted address",
                        peer.IP, peer.Port);

            peer.DisconnectNow(3000u);
        }

        private static void DisconnectOnChecksumFail(ref Peer peer, bool isServer)
        {
            Console.WriteLine("[ERROR] Rejecting new connection from {0}:{1}, Reason: Checksum validation failed",
                peer.IP, peer.Port);

            // Data uint 1000u means client checksum error, 1001u means server.
            uint data = isServer ? 1001u : 1000u;
            peer.DisconnectNow(data);
        }

        private static void DisconnectOnLoginTokenFail(ref Peer peer, bool isServer)
        {
            Console.WriteLine("[ERROR] Rejecting new connection from {0}:{1}, Reason: Login token validation failed",
                        peer.IP, peer.Port);

            // Data uint 1100u means client login token validation error, 1101u means server.
            uint data = isServer ? 1101u : 1100u;
            peer.DisconnectNow(data);
        }

        private static void DisconnectOnAckFail(ref Peer peer, bool isServer)
        {
            Console.WriteLine("[ERROR] Failed connection to {0}:{1}, Reason: Received invalid connection ACK",
                peer.IP, peer.Port);

            // Data uint 1200u means client ACK error, 1201u means server.
            uint data = isServer ? 1201u : 1200u;
            peer.DisconnectNow(data);
        }

        private static void DisconnectOnUnknownConnection(ref Peer peer)
        {
            Console.WriteLine("[ERROR] Force disconnecting Peer at {0}:{1}, Reason: Message received from unknown Peer",
                peer.IP, peer.Port);

            // Data uint 2500u means message received from unknown Peer.
            peer.DisconnectNow(2500u);
        }

        private void BlacklistPeer(ref Peer peer, string key)
        {
            if (BlacklistMap.TryGetValue(key, out BlacklistData? blacklistData))
            {
                blacklistData.Reblacklist();
            }
            else
            {
                BlacklistMap[key] = new BlacklistData(peer.IP, peer.Port);
            }
        }
    }
}
