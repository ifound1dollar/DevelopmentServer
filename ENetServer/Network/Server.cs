using ENet;
using ENetServer.NetObjects;
using ENetServer.Network;
using System;
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
        private readonly ConcurrentQueue<ActionSendObject> gameToNetQueue;
        private readonly ConcurrentQueue<ActionRecvObject> netToGameQueue;
        private readonly ConcurrentQueue<MessageSendObject> serializeToNetQueue;
        private readonly ConcurrentQueue<MessageRecvObject> netToSerializeQueue;

        /// <summary>
        /// Constructs a Server object with references to network concurrent queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Server(ConcurrentQueue<ActionSendObject> gameToNetQueue,
            ConcurrentQueue<ActionRecvObject> netToGameQueue,
            ConcurrentQueue<MessageSendObject> serializeToNetQueue,
            ConcurrentQueue<MessageRecvObject> netToSerializeQueue)
        {
            this.gameToNetQueue = gameToNetQueue;
            this.netToGameQueue = netToGameQueue;
            this.serializeToNetQueue = serializeToNetQueue;
            this.netToSerializeQueue = netToSerializeQueue;
        }



        /// <summary>
        /// Map of all connected peers in form ID:PeerConnection. Contains both clients and servers.
        /// </summary>
        private Dictionary<uint, PeerConnection> Connections { get; } = new();
        private HashSet<Peer> AllPeers { get; } = new();

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

            // Disconnect all Connections and wait 3 seconds for ACKs.
            DisconnectAllOnStop(serverHost);

            // Finally, flush and dispose server host.
            serverHost.Flush();
            serverHost.Dispose();
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
        /// Handles send tasks - dequeues outgoing tasks, queues ENet operations.
        /// </summary>
        internal void DoSendTasks()
        {
            // TODO: CHANGE THIS TO GET SNAPSHOT AND LOOP THAT MANY TIMES, THEN MOVE ON
            //  BUT NOT YET, TEST BEFORE DOING THIS
            // The reason that send/receive operations during stress test are so mismatched
            //  may be because the network thread was getting stuck pulling from the send
            //  queue.

            // Action send queue.
            while (!gameToNetQueue.IsEmpty)
            {
                //Console.WriteLine("Reading from GameToNetQueue");

                // Try dequeue, then operate based on ActionType if successful.
                if (!gameToNetQueue.TryDequeue(out ActionSendObject? actionSendObject)) break;

                switch (actionSendObject.ActionType)
                {
                    case ActionType.Connect_One:
                        {
                            QueueConnectOne(actionSendObject);
                            break;
                        }
                    case ActionType.Disconnect_One:
                        {
                            QueueDisconnectOne(actionSendObject);
                            break;
                        }
                    case ActionType.Disconnect_Many:
                        {
                            QueueDisconnectMany(actionSendObject);
                            break;
                        }
                    case ActionType.Disconnect_All:
                        {
                            QueueDisconnectAll(actionSendObject);
                            break;
                        }
                    // DO NOTHING FOR DEFAULT CASE
                }

                /* - outside of switch case, inside while loop - */
            }

            // Message receive queue.
            while (!serializeToNetQueue.IsEmpty)
            {
                //Console.WriteLine("Reading from SerializeToNetQueue");

                // Try dequeue, then operate based on MessageType if successful.
                if (!serializeToNetQueue.TryDequeue(out MessageSendObject? messageSendObject)) break;

                switch (messageSendObject.MessageType)
                {
                    case MessageType.Message_One:
                        {
                            QueueMessageOne(messageSendObject);
                            break;
                        }
                    case MessageType.Message_Many:
                        {
                            QueueMessageMany(messageSendObject);
                            break;
                        }
                    case MessageType.Message_All:
                        {
                            QueueMessageAll(messageSendObject);
                            break;
                        }
                    case MessageType.Message_All_Except:
                        {
                            QueueMessageAllExcept(messageSendObject);
                            break;
                        }
                    case MessageType.TestMessage:
                        {
                            //TODO: REMOVE THIS TEST CASE
                            Connections.GetValueOrDefault(messageSendObject.PeerParams.ID); // SIMULATE GETTING PEER FOR ACTUAL SEND

                            if (messageSendObject.Bytes != null)
                            {
                                Connection tempConnection = new(true);
                                MessageRecvObject messageReceiveObject = MessageRecvObject.Factory.CreateFromTestRecv(
                                    tempConnection, messageSendObject.Bytes);
                                netToSerializeQueue.Enqueue(messageReceiveObject);
                            }
                            break;
                        }
                        // DO NOTHING FOR DEFAULT CASE
                }

                /* - outside of switch case, inside while loop - */
            }
        }

        /// <summary>
        /// Handles receive tasks - handles ENet events and runs host service, adds to incoming queues.
        /// </summary>
        internal void DoReceiveTasks()
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
        /// <param name="actionSendObject"> ActionSendObject containing relevant remote host data. </param>
        internal void QueueConnectOne(ActionSendObject actionSendObject)
        {
            string ip = actionSendObject.PeerParams.IP;
            ushort port = actionSendObject.PeerParams.Port;

            // Verify port is within valid range (outbound connections can only ever be to servers).
            if (port < ServerPortMin && port >= ClientPortMin)
            {
                Console.WriteLine("[ERROR] Specified port out of range for new Connect attempt. Valid range: {0}-{1}",
                    ServerPortMin, ClientPortMin - 1);
                return;
            }

            // Verify not trying to connect to a Host already connected to.
            foreach (var peerConnection in Connections)
            {
                if (peerConnection.Value.Connection.IP == ip && peerConnection.Value.Connection.Port == port)
                {
                    Console.WriteLine("[ERROR] Attempted to connect to an existing Connection. Aborting.");
                    return;
                }
            }

            // Create Address object with IP and Port from ActionSendObject.
            Address remoteAddress = new();
            remoteAddress.SetIP(actionSendObject.PeerParams.IP);
            remoteAddress.Port = actionSendObject.PeerParams.Port;

            // Queue connect to remote address.
            try
            {
                Peer? pendingPeer = serverHost?.Connect(remoteAddress);
                if (pendingPeer != null)
                {
                    // If connect attempt did not fail, set lower timeout value and add to AllPeers.
                    pendingPeer.Value.Timeout(32, 5000, 10000); //32 and 5000 are default, last param default is 30000 (30s)
                    AllPeers.Add(pendingPeer.Value);
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
        /// <param name="actionSendObject">  </param>
        internal void QueueDisconnectOne(ActionSendObject actionSendObject)
        {
            // Only if a peer with this ID is found.
            if (Connections.TryGetValue(actionSendObject.PeerParams.ID, out PeerConnection? peerConnection))
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Peer;
                if (peer.State != PeerState.Connected) return;

                // Disconnect with default data value.
                peer.Disconnect(0u);
            }
        }

        /// <summary>
        /// Queues disconnect requests to all remote hosts with IDs contained in the ActionSendObject.
        /// </summary>
        /// <param name="actionSendObject">  </param>
        internal void QueueDisconnectMany(ActionSendObject actionSendObject)
        {
            // Iterate over IDArray and try to disconnect any valid Connection with the ID.
            foreach (var id in actionSendObject.PeerParams.IDArray)
            {
                // Only if a peer with this ID is found.
                if (Connections.TryGetValue(id, out PeerConnection? peerConnection))
                {
                    // Verify that the peer is valid.
                    Peer peer = peerConnection.Peer;
                    if (peer.State != PeerState.Connected) return;

                    // Disconnect with default data value.
                    peer.Disconnect(0u);
                }
            }
        }

        /// <summary>
        /// Queues disconnect requests to be sent to all connected remote hosts matching
        ///  the HostType contained in the ActionSendObject.
        /// </summary>
        /// <param name="actionSendObject">  </param>
        internal void QueueDisconnectAll(ActionSendObject actionSendObject)
        {
            HostType hostType = actionSendObject.PeerParams.HostType;

            // If HostType is both, do not do any filtering.
            if (hostType == HostType.Both)
            {
                foreach (var peerConnection in Connections)
                {
                    // Verify that the peer is valid.
                    Peer peer = peerConnection.Value.Peer;
                    if (peer.State != PeerState.Connected) continue;

                    // Disconnect with default data value.
                    peer.Disconnect(0u);
                }
            }
            // Else if HostType is exclusively one or the other, filter each Connection.
            else if (hostType == HostType.Server || hostType == HostType.Client)
            {
                bool isServer = (actionSendObject.PeerParams.HostType != HostType.Server);
                foreach (var peerConnection in Connections)
                {
                    // Skip wrong HostType peers.
                    if (peerConnection.Value.Connection.IsServer == isServer) continue;

                    Peer peer = peerConnection.Value.Peer;
                    if (peer.State != PeerState.Connected) continue;

                    peer.Disconnect(0u);
                }
            }
        }

        #endregion
        #region Send Methods

        /// <summary>
        /// Sends a packet to a single peer.
        /// </summary>
        /// <param name="messageSendObject"> MessageSendObject containing relevant peer and payload data. </param>        
        internal void QueueMessageOne(MessageSendObject messageSendObject)
        {
            // Create packet from passed-in byte[], which is already in ready-to-send format.
            Packet packet = default;
            packet.Create(messageSendObject.Bytes);

            // Only if a peer with this ID is found.
            if (Connections.TryGetValue(messageSendObject.PeerParams.ID, out PeerConnection? peerConnection))
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Peer;
                if (peer.State != PeerState.Connected) return;

                // TEMP send on channel 0.
                peer.Send(0, ref packet);
            }
        }

        /// <summary>
        /// Queues messages to be sent to all remote hosts with IDs contained in the MessageSendObject.
        /// </summary>
        /// <param name="messageSendObject">  </param>
        internal void QueueMessageMany(MessageSendObject messageSendObject)
        {
            // Create packet from passed-in byte[], which is already in ready-to-send format.
            Packet packet = default;
            packet.Create(messageSendObject.Bytes);

            // Iterate over IDArray and try to message any valid Connection with the ID.
            foreach (var id in messageSendObject.PeerParams.IDArray)
            {
                // Only if a peer with this ID is found.
                if (Connections.TryGetValue(id, out PeerConnection? peerConnection))
                {
                    // Verify that the peer is valid.
                    Peer peer = peerConnection.Peer;
                    if (peer.State != PeerState.Connected) return;

                    // TEMP send on channel 0.
                    peer.Send(0, ref packet);
                }
            }
        }

        /// <summary>
        /// Sends a packet to all connected peers.
        /// </summary>
        /// <param name="messageSendObject"> MessageSendObject containing relevant peer and payload data. </param>        
        internal void QueueMessageAll(MessageSendObject messageSendObject)
        {
            // Create packet from passed-in byte[], which is already in ready-to-send format.
            Packet packet = default;
            packet.Create(messageSendObject.Bytes);

            HostType hostType = messageSendObject.PeerParams.HostType;

            // If HostType is both, do not do any filtering.
            if (hostType == HostType.Both)
            {
                foreach (var peerConnection in Connections)
                {
                    // Verify that the peer is valid.
                    Peer peer = peerConnection.Value.Peer;
                    if (peer.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Send(0, ref packet);
                }
            }
            // Else if HostType is exclusively one or the other, filter each Connection.
            else if (hostType == HostType.Server || hostType == HostType.Client)
            {
                bool isServer = (messageSendObject.PeerParams.HostType == HostType.Server);
                foreach (var peerConnection in Connections)
                {
                    // Skip wrong HostType peers.
                    if (peerConnection.Value.Connection.IsServer != isServer) continue;

                    Peer peer = peerConnection.Value.Peer;
                    if (peer.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Send(0, ref packet);
                }
            }
        }

        /// <summary>
        /// Sends a packet to all connected peers except one.
        /// </summary>
        /// <param name="messageSendObject"> MessageSendObject containing relevant peer and payload data. </param>        
        internal void QueueMessageAllExcept(MessageSendObject messageSendObject)
        {
            // Create packet from passed-in byte[], which is already in ready-to-send format.
            Packet packet = default;
            packet.Create(messageSendObject.Bytes);

            HostType hostType = messageSendObject.PeerParams.HostType;

            // If HostType is both, do not do any filtering.
            if (hostType == HostType.Both)
            {
                foreach (var peerConnection in Connections)
                {
                    // Skipped passed-in peer ID.
                    if (peerConnection.Key == messageSendObject.PeerParams.ID) continue;

                    // Verify that the peer is valid.
                    Peer peer = peerConnection.Value.Peer;
                    if (peer.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Send(0, ref packet);
                }
            }
            // Else if HostType is exclusively one or the other, filter each Connection.
            else if (hostType == HostType.Server || hostType == HostType.Client)
            {
                bool isServer = (messageSendObject.PeerParams.HostType == HostType.Server);
                foreach (var peerConnection in Connections)
                {
                    // Skip wrong HostType peers AND passed-in peer ID.
                    if (peerConnection.Value.Connection.IsServer != isServer) continue;
                    if (peerConnection.Key == messageSendObject.PeerParams.ID) continue;

                    Peer peer = peerConnection.Value.Peer;
                    if (peer.State != PeerState.Connected) continue;

                    // TEMP send on channel 0.
                    peer.Send(0, ref packet);
                }
            }
        }

        #endregion

        #region Receive Event Handlers

        private void HandleConnectEvent(ref Event connectEvent)
        {
            // If below client port minimum, is server (server min (7777) is always less than client min (8888)).
            bool isServer = connectEvent.Peer.Port < ClientPortMin;

            // Create new PeerConnection (which creates a new Connection) and add to map.
            PeerConnection peerConnection = new(connectEvent.Peer, isServer);
            Connections.Add(peerConnection.Peer.ID, peerConnection);

            AllPeers.Add(connectEvent.Peer);

            // Enqueue connect object with new peer's Connection for use by game thread.
            ActionRecvObject actionRecvObject = ActionRecvObject.Factory.CreateFromConnect(
                peerConnection.Connection);
            netToGameQueue.Enqueue(actionRecvObject);
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent)
        {
            // Remove PeerConnection from map and enqueue Connection if successful.
            if (Connections.Remove(disconnectEvent.Peer.ID, out PeerConnection? peerConnection))
            {
                // Enqueue disconnect object with disconnected peer's Connection for use by game thread.
                ActionRecvObject actionRecvObject = ActionRecvObject.Factory.CreateFromDisconnect(
                    peerConnection.Connection);
                netToGameQueue.Enqueue(actionRecvObject);
            }
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent)
        {
            // Remove PeerConnection from map and enqueue Connection if successful.
            if (Connections.Remove(timeoutEvent.Peer.ID, out PeerConnection? peerConnection))
            {
                // Enqueue timeout object with timed-out peer's Connection for use by game thread.
                ActionRecvObject actionRecvObject = ActionRecvObject.Factory.CreateFromTimeout(
                    peerConnection.Connection);
                netToGameQueue.Enqueue(actionRecvObject);
            }
        }

        private void HandleReceiveEvent(ref Event receiveEvent)
        {
            // Copy packet payload into byte[].
            byte[] bytes = new byte[receiveEvent.Packet.Length];
            receiveEvent.Packet.CopyTo(bytes);

            // Get PeerConnection from map and enqueue Connection if successful.
            if (Connections.TryGetValue(receiveEvent.Peer.ID, out PeerConnection? peerConnection))
            {
                // Enqueue MessageRecvObject with this peer's Connection and the byte[] payload.
                MessageRecvObject messageRecvObject = MessageRecvObject.Factory.CreateFromMessage(
                    peerConnection.Connection, bytes);
                netToSerializeQueue.Enqueue(messageRecvObject);
            }

            // Always dispose packet after handling receive, even if did not enqueue NetRecvObject.
            receiveEvent.Packet.Dispose();
        }

        #endregion

    }
}
