using ENet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer
{
    internal class Server
    {
        private readonly Host serverHost;
        private readonly Address address;
        private readonly int peerLimit;
        private readonly int channelLimit;

        /// <summary>
        /// Constructs a server with optional parameters.
        /// </summary>
        /// <param name="ip"> IP address the server will run on (irrelevant for server). </param>
        /// <param name="port"> Port that the server will listen on. </param>
        /// <param name="peerLimit"> Maximum number of peers that can be connected at once (library limits to 4096). </param>
        /// <param name="channelLimit"> Maximum number of channels that can be used for communication with this server. </param>
        internal Server(string ip = "127.0.0.1", ushort port = 7777, int peerLimit = 64, int channelLimit = 2)
        {
            // Construct Host server, but not yet fully created and listening.
            serverHost = new();

            // Address for this server host.
            address = new();
            address.SetIP(ip);          // Can optionally set specific IP to run on (not required for server)
            address.Port = port;        // Actual port server will run on

            // Server creation parameters.
            this.peerLimit = peerLimit;
            this.channelLimit = channelLimit;
        }



        /// <summary>
        /// Map of all connected clients in form ID:Client.
        /// </summary>
        private Dictionary<uint, Peer> Peers { get; set; } = new();

        internal Address GetAddress()
        {
            return address;
        }



        #region Start / Stop / Run Operations

        /// <summary>
        /// Starts up server to begin listening on the designated port.
        /// </summary>
        internal void Start()
        {
            // Create server Host with address and args.
            serverHost.Create(address, peerLimit, channelLimit, 0u, 0u, 1024 * 1024);   // 1024*1024 is maximum buffer size in enet.h
        }

        /// <summary>
        /// Performs last-minute operations (i.e. disconnecting all clients) before shutting down server host.
        /// </summary>
        /// <param name="netRecvQueue"> Reference to ConcurrentQueue for network receives. </param>
        internal void Stop(ref ConcurrentQueue<NetworkManager.NetworkRecvObject> netRecvQueue)
        {
            // Disconnect all clients before disposing server (graceful disconnects).
            QueueDisconnectAll();

            // Wait 3 seconds for clients to response to disconnect request.
            while (serverHost.Service(3000, out Event netEvent) > 0)
            {
                switch (netEvent.Type)
                {
                    case EventType.Disconnect:
                        HandleDisconnectEvent(ref netEvent, ref netRecvQueue);
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
        /// Handles events and runs host service. Blocks until all operations have been completed.
        /// </summary>
        /// <param name="netRecvQueue"> Reference to ConcurrentQueue for network receives. </param>
        internal void DoENetTasks(ref ConcurrentQueue<NetworkManager.NetworkRecvObject> netRecvQueue)
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
                        HandleConnectEvent(ref netEvent, ref netRecvQueue);
                        break;

                    case EventType.Disconnect:
                        HandleDisconnectEvent(ref netEvent, ref netRecvQueue);
                        break;

                    case EventType.Timeout:
                        HandleTimeoutEvent(ref netEvent, ref netRecvQueue);
                        break;

                    case EventType.Receive:
                        HandleReceiveEvent(ref netEvent, ref netRecvQueue);
                        break;
                }
            }
        }

        #endregion

        #region Event Handlers

        private void HandleConnectEvent(ref Event connectEvent, ref ConcurrentQueue<NetworkManager.NetworkRecvObject> netRecvQueue)
        {
            // Add peer to Peers map.
            Peer peer = connectEvent.Peer;
            Peers.Add(peer.ID, peer);

            // Enqueue connect event for use by other threads.
            NetworkManager.NetworkRecvObject netObject = NetHelpers.CreateNetworkRecvObject(
                connectEvent.Peer.ID, connectEvent.Peer.IP, connectEvent.Peer.Port, [], NetworkManager.RecvType.CONNECT);
            netRecvQueue.Enqueue(netObject);
        }

        private void HandleDisconnectEvent(ref Event disconnectEvent, ref ConcurrentQueue<NetworkManager.NetworkRecvObject> netRecvQueue)
        {
            // Remove peer from Peers map.
            Peer peer = disconnectEvent.Peer;
            Peers.Remove(peer.ID);

            // Enqueue disconnect event for use by other threads.
            NetworkManager.NetworkRecvObject netObject = NetHelpers.CreateNetworkRecvObject(
                disconnectEvent.Peer.ID, disconnectEvent.Peer.IP, disconnectEvent.Peer.Port, [], NetworkManager.RecvType.DISCONNECT);
            netRecvQueue.Enqueue(netObject);
        }

        private void HandleTimeoutEvent(ref Event timeoutEvent, ref ConcurrentQueue<NetworkManager.NetworkRecvObject> netRecvQueue)
        {
            // Remove peer from Peers map.
            Peer peer = timeoutEvent.Peer;
            Peers.Remove(peer.ID);

            // Enqueue timeout event for use by other threads.
            NetworkManager.NetworkRecvObject netObject = NetHelpers.CreateNetworkRecvObject(
                timeoutEvent.Peer.ID, timeoutEvent.Peer.IP, timeoutEvent.Peer.Port, [], NetworkManager.RecvType.TIMEOUT);
            netRecvQueue.Enqueue(netObject);
        }

        private void HandleReceiveEvent(ref Event receiveEvent, ref ConcurrentQueue<NetworkManager.NetworkRecvObject> netRecvQueue)
        {
            // Copy packet payload into byte[].
            byte[] bytes = new byte[receiveEvent.Packet.Length];
            receiveEvent.Packet.CopyTo(bytes);

            // Enqueue NetworkRecvObject with data from this receive event.
            NetworkManager.NetworkRecvObject netObject = NetHelpers.CreateNetworkRecvObject(
                receiveEvent.Peer.ID, receiveEvent.Peer.IP, receiveEvent.Peer.Port, bytes, NetworkManager.RecvType.MESSAGE);
            netRecvQueue.Enqueue(netObject);

            // Dispose packet after handling.
            receiveEvent.Packet.Dispose();
        }

        #endregion

        #region Send Functions

        /// <summary>
        /// Sends a packet to a single peer.
        /// </summary>
        /// <param name="peerId"> ID of Peer to send packet to. </param>
        /// <param name="packet"> Packet to send to Peer. </param>
        internal void QueueSendOne(uint peerId, Packet packet)
        {
            // Return if a peer with this ID does not exist.
            if (!Peers.TryGetValue(peerId, out Peer peer)) return;

            // Verify that the peer is valid.
            if (peer.State != PeerState.Connected) return;

            // Temp send on channel 0.
            peer.Send(0, ref packet);
        }

        /// <summary>
        /// Sends a packet to all connected peers.
        /// </summary>
        /// <param name="packet"> Packet to send to Peer. </param>
        internal void QueueSendAll(Packet packet)
        {
            // Iterate over all clients, sending packet to all except matching.
            foreach (var peer in Peers)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // Temp send on channel 0.
                peer.Value.Send(0, ref packet);
            }
        }

        /// <summary>
        /// Sends a packet to all connected peers except one.
        /// </summary>
        /// <param name="peerId"> ID of Peer being excluded from packet send. </param>
        /// <param name="packet"> Packet to send to Peer. </param>
        internal void QueueSendAllExcept(uint peerId, Packet packet)
        {
            // Iterate over all clients, sending packet to all except matching.
            foreach (var peer in Peers)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // Skip peer passed in as argument.
                if (peer.Key == peerId) continue;

                // Temp send on channel 0.
                peer.Value.Send(0, ref packet);
            }
        }

        #endregion

        #region Connect / Disconnect Functions

        /// <summary>
        /// Queues disconnect requests to be sent to all connected clients next tick.
        /// </summary>
        /// <param name="peerId"> ID of Peer to disconnect. </param>
        internal void QueueDisconnectOne(uint peerId)
        {
            // Return if a Peer with this ID is not found.
            if (!Peers.TryGetValue(peerId, out Peer peer)) return;

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
            foreach (var peer in Peers)
            {
                // Verify that the peer is valid.
                if (peer.Value.State != PeerState.Connected) continue;

                // Disconnect with default data value.
                peer.Value.Disconnect(0);
            }
        }

        #endregion
    }
}
