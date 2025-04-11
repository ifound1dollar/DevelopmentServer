using ENet;
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
using ENetServer.Network.Data;

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
        private Dictionary<uint, PeerData> Servers { get; } = new();
        private Dictionary<string, PeerData> InitiatedPeers { get; } = new();

        private Validator Validator { get; } = new Validator(isServer: false);

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
            // Also implicitly disables connecting to another client.
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
            foreach (var peerData in Servers)
            {
                // Verify that the peer is valid (using ENet PeerState here).
                if (peerData.Value.Peer.State != PeerState.Connected) continue;

                // Disconnect with uint 300u, sending peer-initiated disconnect on host shutdown.
                peerData.Value.Peer.Disconnect(300u);   // Is received by peer.
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
                                        netSendObject.PeerParams, 0, netSendObject.Bytes, netSendObject.Length);
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

            // Verify port is within valid range (outbound connections can only ever be to game servers).
            if (port < ServerPortMin || port >= ClientPortMin)
            {
                Console.WriteLine("[ERROR] Specified port out of range for new Connect attempt. Valid range: {0}-{1}",
                    ServerPortMin, ClientPortMin - 1);
                return;
            }

            // Verify not trying to connect to a Host already connected to.
            foreach (var peerData in Servers)
            {
                if (peerData.Value.Peer.IP == ip && peerData.Value.Peer.Port == port)
                {
                    Console.WriteLine("[ERROR] Attempted to connect to an existing Connection. Aborting.");
                    return;
                }
            }

            // Create Address object with IP and Port from NetSendObject.
            Address remoteAddress = new();
            remoteAddress.SetIP(ip);
            remoteAddress.Port = port;

            try
            {
                // Get checksum from login token before connect attempt.
                uint checksum = NetStatics.CalculateChecksum(netSendObject.PeerParams.LoginToken);

                // Actually make connection request, which returns null OR throws an exception on failure.
                Peer? pendingPeer = clientHost?.Connect(remoteAddress, 2, checksum);
                if (pendingPeer != null)
                {
                    pendingPeer.Value.Timeout(32, 5000, 10000); //32 and 5000 are default, last param default is 30000 (30s)

                    // Add this peer to pending peers.
                    string key = NetStatics.GetAddressString(ip, port);
                    PeerData peerData = new((Peer)pendingPeer, PeerData.CustomState.Initiated);
                    peerData.SetLoginToken(netSendObject.PeerParams.LoginToken);
                    InitiatedPeers[key] = peerData;
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
            if (Servers.TryGetValue(netSendObject.PeerParams.ID, out PeerData? peerData))
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
                if (Servers.TryGetValue(id, out PeerData? peerData))
                {
                    // Verify that the peer is valid (using ENet PeerState here).
                    if (peerData.Peer.State != PeerState.Connected) return;

                    // Disconnect with data value.
                    peerData.Peer.Disconnect(netSendObject.Data);
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
            foreach (var peerData in Servers)
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
            if (Servers.TryGetValue(netSendObject.PeerParams.ID, out PeerData? peerData))
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
            foreach (var id in netSendObject.PeerParams.IDArray)
            {
                // Only if a peer with this ID is found.
                if (Servers.TryGetValue(id, out PeerData? peerData))
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

            // Do not filter based on HostType because as a client, all remote hosts will be servers.
            foreach (var peerData in Servers)
            {
                // Verify that the peer is valid (using ENet PeerState here).
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
            string key = NetStatics.GetAddressString(connectEvent.Peer.IP, connectEvent.Peer.Port);

            // If new connection is from another client, immediately disconnect that Peer.
            // OR if new connection is not in InitiatedPeers (meaning this client did not initiate), disconnect.
            if (connectEvent.Peer.Port >= ClientPortMin || !InitiatedPeers.ContainsKey(key))
            {
                DisconnectOnUnknownNewConnection(ref peer);
            }

            // Else new connection was from a server, so handle new server connection.
            ProcessIncomingConnection(ref peer, key);
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            Peer peer = disconnectEvent.Peer;
            string key = NetStatics.GetAddressString(peer.IP, peer.Port);

            // If was in InitiatedPeers, then note failure to connect.
            if (InitiatedPeers.Remove(key))
            {
                Console.WriteLine("[ERROR] Failed to connect to server at address {0}:{1}, Code: {2}",
                    peer.IP, peer.Port, disconnectEvent.Data);
            }

            // Remove Peer from map and enqueue if successful.
            if (Servers.Remove(peer.ID))
            {
                // If Data uint is 0, then event is an automatic ACK from disconnect initialized here.
                uint data = (disconnectEvent.Data == 0) ? 201u : disconnectEvent.Data;

                // Enqueue disconnect object with disconnected peer's data for use by other threads.
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromDisconnect(
                    peerParams, disconnectEvent.ChannelID, data);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent)
        {
            Peer peer = timeoutEvent.Peer;

            // Remove Peer from map and enqueue if successful.
            if (Servers.Remove(timeoutEvent.Peer.ID))
            {
                // Enqueue timeout object with timed-out peer's data for use by other threads.
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromTimeout(
                    peerParams, timeoutEvent.ChannelID, 400u);
                netRecvQueue.Enqueue(dataObject);
            }
        }

        private void HandleReceiveEvent(ref Event receiveEvent)
        {
            Peer peer = receiveEvent.Peer;

            // Copy packet payload into byte[].
            int length = receiveEvent.Packet.Length;
            byte[] bytes = new byte[length];
            receiveEvent.Packet.CopyTo(bytes);

            // If message received from Peer which is not in map, this Peer is not validated.
            if (!Servers.ContainsKey(peer.ID))
            {
                ProcessValidationAck(ref peer, bytes, length);
            }
            // Else is in map (validated), so process as valid message.
            else
            {
                ProcessRegularMessage(ref peer, receiveEvent.ChannelID, bytes, length);
            }

            // Always dispose packet after handling receive, even if did not enqueue NetRecvObject.
            receiveEvent.Packet.Dispose();
        }

        #endregion

        #region Connect processing

        private void ProcessIncomingConnection(ref Peer peer, string key)
        {
            // New connection is guaranteed to have been self-initiated (clients disallow incoming connections).
            if (InitiatedPeers.TryGetValue(key, out PeerData? peerData))
            {
                string token = peerData.GetLoginToken();
                if (!string.IsNullOrEmpty(token))
                {
                    // Create packet with login token and send.
                    Packet packet = default;
                    packet.Create(NetStatics.GetBytes(token));
                    peer.Send(0, ref packet);
                }
                else
                {
                    // If cannot find login token, disconnect immediately and log error.
                    DisconnectOnMissingLoginToken(ref peer);
                }
            }
            else
            {
                // Else new connection from unknown Peer, so disconnect immediately.
                DisconnectOnUnknownNewConnection(ref peer);
            }
        }

        #endregion

        #region Message processing

        private void ProcessRegularMessage(ref Peer peer, byte channelId, byte[] bytes, int length)
        {
            // Enqueue NetRecvObject with this peer's data.
            PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
            NetRecvObject dataObject = NetRecvObject.Factory.CreateFromMessage(peerParams, channelId, bytes, length);
            netRecvQueue.Enqueue(dataObject);
        }

        private void ProcessValidationAck(ref Peer peer, byte[] bytes, int length)
        {
            string key = NetStatics.GetAddressString(peer.IP, peer.Port);

            // If Peer does not exist in InitiatedPeers, then received message from unknown connection.
            if (!InitiatedPeers.TryGetValue(key, out PeerData? peerData))
            {
                DisconnectOnMessageFromUnknownPeer(ref peer);
                return;
            }

            // Get ACK from packet.
            string str = NetStatics.GetString(bytes, 0, length);
            str = NetStatics.FormatStringFromReceive(str);

            // If validation response contains expected data, add Peer to map (now valid)
            if (Validator.CompareValidationAck(str))
            {
                // Remove from InitiatedPeers, and add to fully-connected map.
                InitiatedPeers.Remove(key);
                peerData.State = PeerData.CustomState.Connected;
                Servers[peer.ID] = peerData;

                // Create packet with response ACK and send.
                string ack = Validator.GetAckResponseString();
                Packet packet = default;
                packet.Create(NetStatics.GetBytes(ack));
                peer.Send(0, ref packet);

                // Enqueue connect object with new peer's Connection for use by other threads.
                PeerParams peerParams = new(HostType.Server, peer.ID, peer.IP, peer.Port);
                NetRecvObject dataObject = NetRecvObject.Factory.CreateFromConnect(
                    peerParams, 0, 101u);
                netRecvQueue.Enqueue(dataObject);

                return;
            }

            // Else validation ACK was not expected, so log failure and disconnect.
            DisconnectOnAckFail(ref peer);
        }

        #endregion

        private static void DisconnectOnAckFail(ref Peer peer)
        {
            Console.WriteLine("[ERROR] Failed connection to {0}:{1}, Reason: Received invalid connection ACK",
                peer.IP, peer.Port);

            // Data uint 1200u means client ACK error, which is always called by initiator (this)
            //  so is always client.
            peer.DisconnectNow(1200u);
        }

        private static void DisconnectOnMissingLoginToken(ref Peer peer)
        {
            Console.WriteLine("[ERROR] Aborting new connection attempt to {0}:{1}, Reason: Failed to locate login token",
                        peer.IP, peer.Port);
            peer.DisconnectNow(1300u);  // Always from client
        }

        private static void DisconnectOnUnknownNewConnection(ref Peer peer)
        {
            Console.WriteLine("[ERROR] Rejecting new connection from {0}:{1}, Reason: Disallowed Peer",
                    peer.IP, peer.Port);
            peer.DisconnectNow(2000u);  // Data 2000u is for generic disallowed connection
        }

        private static void DisconnectOnMessageFromUnknownPeer(ref Peer peer)
        {
            Console.WriteLine("[ERROR] Force disconnecting Peer at {0}:{1}, Reason: Message received from unknown Peer",
                peer.IP, peer.Port);

            // Data uint 2500u means message received from unknown Peer.
            peer.DisconnectNow(2500u);
        }

    }
}
