using ENet;
using System.Net;

namespace ENetServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Initialize ENet library.
            if (ENet.Library.Initialize())
            {
                Console.WriteLine("[STARTUP] Successfully initialized ENet library.");
            }
            else
            {
                Console.WriteLine("[ERROR] Failed to initialize ENet library.");
                //Environment.Exit(0);
                return;
            }

            // Setup NetworkManager and start networking threads.
            NetworkManager.Instance.Setup();
            NetworkManager.Instance.StartThreadedOperations();

            // Main thread input loop.
            string? input;
            while (true)
            {
                input = Console.ReadLine()?.ToLower();
                if (input == null) break;

                // Operate on input here, separate thread from server thread.
                if (input == "exit" || input == "e")
                {
                    break;
                }
                else if (input == "dc")
                {
                    ServerDisconnectAllClients();
                }
                else
                {
                    ServerBroadcastToAll(input);
                }
            }

            // After loop exits, stop the worker thread.
            NetworkManager.Instance.StopThreadedOperations();

            // Always de-initialize ENet library on application exit.
            ENet.Library.Deinitialize();
        }



        /// <summary>
        /// Instructs the server to disconnect all connected clients on next Tick.
        /// </summary>
        public static void ServerDisconnectAllClients()
        {
            Console.WriteLine("[ACTION] Disconnecting all clients.");

            NetworkManager.GameOutObject gameOutObject = new NetworkManager.GameOutObject(
                0, "", NetworkManager.SendType.DISCONNECT_ALL);
            NetworkManager.Instance.SendGameDataObject(gameOutObject);
        }

        /// <summary>
        /// Instructs the server to broadcast the passed-in string to all connected clients.
        /// </summary>
        /// <param name="message"> The message string to broadcast. </param>
        public static void ServerBroadcastToAll(string message)
        {
            Console.WriteLine("[ACTION] Broadcasting message \"" + message + "\" to all clients.");

            NetworkManager.GameOutObject gameOutObject = new NetworkManager.GameOutObject(
                0, message, NetworkManager.SendType.MESSAGE_ALL);
            NetworkManager.Instance.SendGameDataObject(gameOutObject);
        }
    }
}
