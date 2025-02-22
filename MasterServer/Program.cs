using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
//using ENet;

namespace MasterServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Let's see if this works xd");

            Server server = new Server(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080));

            //Bind required events for the server.
            //TODO: VERIFY THAT NULLABILITY OF object? sender IS VALID AND NON-PROBLEMATIC
            
            server.OnSendMessage += (object? sender, UniversalMessageEventHandler e) =>
            {
                Console.WriteLine("Client with GUID: {0} | Message sent: {1}", e.Client.Guid, e.Message);
            };
            
            server.OnMessageReceived += (object? sender, UniversalMessageEventHandler e) =>
            {
                Console.WriteLine("Client with GUID: {0} | Message received: {1}", e.Client.Guid, e.Message);
            };

            server.OnClientConnected += (object? sender, UniversalClientEventHandler e) =>
            {
                Console.WriteLine("Client connected with GUID: {0}", e.Client.Guid);
            };

            server.OnClientDisconnected += (object? sender, UniversalClientEventHandler e) =>
            {
                Console.WriteLine("Client disconnected with GUID: {0}", e.Client.Guid);
            };

            int counter = 0;
            while (true)
            {
                Thread.Sleep(1000);
                Client? client = server.GetConnectedClient(0);
                if (client != null)
                {
                    server.SendMessage(client, "Sent message from server: " + counter);
                }

                counter++;
            }

            //Exit the application only when the close button is clicked.
            Process.GetCurrentProcess().WaitForExit();
        }
    }
}
