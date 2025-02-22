using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static MasterServer.Helpers;

namespace MasterServer
{
    public class Client
    {
        public Client(Server server, Socket socket)
        {
            Server = server;
            Socket = socket;
            Guid = Helpers.CreateGuid("client");

            //ASYNC - Start waiting for incoming messages
            Socket.BeginReceive([0], 0, 0, SocketFlags.None, MessageCallback, null);
        }



        #region Properties

        public string Guid { get; private set; }
        public Socket Socket { get; private set; }
        public Server Server { get; private set; }
        public bool IsWaitingForPong { get; private set; }

        #endregion

        #region Field Setters

        public void SetIsWaitingForPong(bool isWaiting)
        {
            IsWaitingForPong = isWaiting;
        }

        #endregion

        #region Callbacks

        /// <summary>Called when a message was received from the client</summary>
        private void MessageCallback(IAsyncResult asyncResult)
        {
            try
            {
                Socket.EndReceive(asyncResult);

                // Read the incomming message (MESSAGE BUFFER SIZE 1KB)
                byte[] messageBuffer = new byte[1024];
                int bytesReceived = Socket.Receive(messageBuffer);

                // Resize the byte array to remove whitespaces 
                if (bytesReceived < messageBuffer.Length)
                {
                    Array.Resize<byte>(ref messageBuffer, bytesReceived);
                }

                //TEMP
                Console.WriteLine(bytesReceived);
                //TEMP

                // Get the opcode of the frame
                EOpcodeType opcode = Helpers.GetFrameOpcode(messageBuffer);

                // If the connection was closed
                if (opcode == EOpcodeType.ClosedConnection)
                {
                    Server.ClientDisconnect(this);
                    return;
                }

                // Pass the message to the server event to handle the logic
                Server.ReceiveMessage(this, Helpers.GetDataFromFrame(messageBuffer));

                // Start to receive messages again
                Socket.BeginReceive([0], 0, 0, SocketFlags.None, MessageCallback, null);

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                Socket.Close();
                Socket.Dispose();
                Server.ClientDisconnect(this);
            }
        }

        #endregion
    }
}
