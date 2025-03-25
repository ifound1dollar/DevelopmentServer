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
        private int peerLimit;
        private int channelLimit;

        // QUEUE REFERENCES
        private readonly ConcurrentQueue<ActionSendObject> gameToNetQueue;
        private readonly ConcurrentQueue<ActionRecvObject> netToGameQueue;
        private readonly ConcurrentQueue<MessageSendObject> serializeToNetQueue;
        private readonly ConcurrentQueue<MessageRecvObject> netToSerializeQueue;

        /// <summary>
        /// Constructs a Client object with references to network concurrent queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Client(ConcurrentQueue<ActionSendObject> gameToNetQueue,
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
        /// Map of all servers this client is connected to in form ID:PeerConnection.
        /// </summary>
        private Dictionary<uint, PeerConnection> Connections { get; } = new();
        private HashSet<Peer> AllPeers { get; } = new();

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
        /// Handles send tasks - dequeues outgoing tasks, queues ENet operations.
        /// </summary>
        internal void DoSendTasks()
        {
            // CHANGE THIS TO GET SNAPSHOT AND LOOP THAT MANY TIMES, THEN MOVE ON
            // BUT NOT YET, TEST BEFORE DOING THIS

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
                    // Do not include Message_All_Except case (irrelevant for Clients).
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
        /// <param name="actionSendObject"> NetSendObject containing relevant server data. </param>
        internal void QueueConnectOne(ActionSendObject actionSendObject)
        {
            //Console.WriteLine("QueueConnectOne called.");

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

            // Create Address object with IP and Port from NetSendObject.
            Address remoteAddress = new();
            remoteAddress.SetIP(actionSendObject.PeerParams.IP);
            remoteAddress.Port = actionSendObject.PeerParams.Port;

            // Queue connect to remote address.
            try
            {
                Peer? pendingPeer = clientHost?.Connect(remoteAddress);
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
        /// Queues disconnect request to be sent to one server next tick.
        /// </summary>
        /// <param name="actionSendObject"> ActionSendObject containing relevant server data. </param>
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
        /// Queues disconnect requests to be sent to all connected remote hosts (servers).
        /// </summary>
        /// <param name="actionSendObject">  </param>
        internal void QueueDisconnectAll(ActionSendObject actionSendObject)
        {
            // Do not filter based on HostType because as a client, all remote hosts will be servers.
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

            // Do not filter based on HostType because as a client, all remote hosts will be servers.
            foreach (var peerConnection in Connections)
            {
                // Verify that the peer is valid.
                Peer peer = peerConnection.Value.Peer;
                if (peer.State != PeerState.Connected) continue;

                // TEMP send on channel 0.
                peer.Send(0, ref packet);
            }
        }

        #endregion

        #region Receive Event Handlers

        private void HandleConnectEvent(ref Event connectEvent)
        {
            //Console.WriteLine("HandleConnectEvent called.");

            // If new connection is from another client, immediately disconnect that Peer.
            if (connectEvent.Peer.Port >= ClientPortMin)
            {
                connectEvent.Peer.Disconnect(0u);   // THIS GUARANTEES ALL PEERS/CONNECTIONS ARE SERVERS
            }

            // Create new PeerConnection (which creates a new Connection) and add to map.
            PeerConnection peerConnection = new(connectEvent.Peer, true);   // Always servers.
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

            // Enqueue NetRecvObject with this peer's Connection ONLY IF an entry for this peer exists in map.
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
