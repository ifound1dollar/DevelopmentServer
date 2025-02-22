using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    public class Server
    {
        public Server(IPEndPoint endpoint)
        {
            //Return if endpoint is null???
            //if (endpoint == null) return;
            Endpoint = endpoint;

            //Create a new listen socket. WebSocket operates as a stream over TCP.
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("WebSocket server listening on {0}:{1}", endpoint.Address.ToString(), endpoint.Port);

            //Start the server.
            Start();
        }



        #region Properties
        public Socket Socket { get; private set; }
        public IPEndPoint Endpoint { get; private set; }
        private List<Client> Clients { get; } = new();

        #endregion

        #region Field Getters

        public Client? GetConnectedClient(int index)
        {
            if (index < 0 || index >= Clients.Count)
            {
                return null;
            }
            return Clients[index];
        }

        public Client? GetConnectedClient(string guid)
        {
            //Searches for a client with matching GUID. Uses custom predicate statement lambda
            // to search for matching value. Method returns null (default value of complex object)
            // if no match is found.
            return Clients.Find((Client client) =>
            {
                return client.Guid == guid;
            });
        }

        public Client? GetConnectedClient(Socket socket)
        {
            //Searches for a client with matching Socket object. Uses custom predicate statement
            // lambda to search for matching value. Method returns null (default value of complex
            // object) if no match is found.
            return Clients.Find((Client client) =>
            {
                return client.Socket == socket;
            });
        }

        public int GetConnectedClientCount()
        {
            return Clients.Count;
        }

        #endregion

        #region Methods

        private void Start()
        {
            //Bind the socket and start listening. WHAT DOES THIS MEAN EXACTLY?
            Socket.Bind(Endpoint);
            Socket.Listen(0);

            //ASYNC - Start accepting clients and incoming connections.
            Socket.BeginAccept(ConnectionCallback, null);
        }

        public void Stop()
        {
            //Close the server socket and dispose.
            Socket.Close();
            Socket.Dispose();
        }

        public void ReceiveMessage(Client client, string message)
        {
            if (OnMessageReceived == null)
            {
                throw new Exception("Server error: event OnMessageReceived is not bound!");
            }
            OnMessageReceived(this, new UniversalMessageEventHandler(client, message));
        }

        public void SendMessage(Client client, string data)
        {
            byte[] frameMessage = Helpers.GetFrameFromString(data);

            client.Socket.Send(frameMessage);

            if (OnSendMessage == null)
            {
                throw new Exception("Server error: event OnSendMessage is not bound!");
            }
            OnSendMessage(this, new UniversalMessageEventHandler(client, data));
        }

        public void ClientDisconnect(Client client)
        {
            //Remove this client from the List of connected clients.
            Clients.Remove(client);

            //Fire the OnClientDisconnected event
            if (OnClientDisconnected == null)
            {
                throw new Exception("Server error: event OnClientDisconnected is not bound!");
            }
            OnClientDisconnected(this, new UniversalClientEventHandler(client));
        }

        #endregion

        #region Callbacks

        private void ConnectionCallback(IAsyncResult asyncResult)
        {
            try
            {
                // Gets the client thats trying to connect to the server
                Socket clientSocket = Socket.EndAccept(asyncResult);

                // Read the handshake updgrade request
                byte[] handshakeBuffer = new byte[1024];
                int handshakeReceived = clientSocket.Receive(handshakeBuffer);

                // Get the hanshake request key and get the hanshake response
                string requestKey = Helpers.GetHandshakeRequestKey(Encoding.Default.GetString(handshakeBuffer));
                string handshakeResponse = Helpers.GetHandshakeResponse(Helpers.HashKey(requestKey));

                //TEMP
                Console.WriteLine("connectionCallback debug: " + requestKey + "\n" + handshakeResponse);
                //TEMP

                // Send the handshake updgrade response to the connecting client 
                clientSocket.Send(Encoding.Default.GetBytes(handshakeResponse));

                // Create a new client object and add 
                // it to the list of connected clients
                Client client = new Client(this, clientSocket);
                Clients.Add(client);

                // Call the event when a client has connected to the listen server 
                if (OnClientConnected == null)
                {
                    throw new Exception("Server error: event OnClientConnected is not bound!");
                }
                OnClientConnected(this, new UniversalClientEventHandler(client));

                // Start to accept incoming connections again 
                Socket.BeginAccept(ConnectionCallback, null);

            }
            catch (Exception Exception)
            {
                Console.WriteLine("An error has occured while trying to accept a connecting client.\n\n{0}", Exception.Message);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>Called after a message was sent</summary>
        public event EventHandler<UniversalMessageEventHandler>? OnSendMessage;

        /// <summary>Called when a message was received from a connected client</summary>
        public event EventHandler<UniversalMessageEventHandler>? OnMessageReceived;
        
        /// <summary>Called when a client was connected to the server (after handshake)</summary>
        public event EventHandler<UniversalClientEventHandler>? OnClientConnected;

        /// <summary>Called when a client disconnected</summary>
        public event EventHandler<UniversalClientEventHandler>? OnClientDisconnected;

        #endregion
    }
}
