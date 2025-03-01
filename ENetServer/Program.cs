using ENet;
using ENetServer.DataObjects;
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
                else if (input == "tr")
                {
                    ServerSendTransformToAll(uint.MaxValue, [-6546515611561564564, 2, 3], [4, 5, 6], [7, 8, 9]);
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

            GameOutDataObject gameOutObject = GameOutDataObject.MakeGenericDisconnectAll();
            NetworkManager.Instance.SendGameDataObject(gameOutObject);
        }

        /// <summary>
        /// Instructs the server to broadcast the passed-in string to all connected clients.
        /// </summary>
        /// <param name="message"> The message string to broadcast. </param>
        public static void ServerBroadcastToAll(string message)
        {
            Console.WriteLine("[ACTION] Broadcasting message \"" + message + "\" to all clients.");

            GameOutDataObject gameOutObject = GameOutDataObject.MakeGenericMessageAll(message);
            NetworkManager.Instance.SendGameDataObject(gameOutObject);
        }

        /// <summary>
        /// Instructs the server to send a transform object to all connected clients.
        /// </summary>
        /// <param name="actorId"> ID of Actor corresponding to the transform. </param>
        /// <param name="location"> Location component of the transform. </param>
        /// <param name="rotation"> Rotation component of the transform. </param>
        /// <param name="scale"> Scale component of the transform. </param>
        public static void ServerSendTransformToAll(uint actorId, double[] location, double[] rotation, double[] scale)
        {
            Console.WriteLine("[ACTION] Broadcasting transform {0}, {1}, {2} for actor with ID {3} to all clients.",
                location.ToString(), rotation.ToString(), scale.ToString(), actorId);

            GameOutDataObject gameOutObject = GameOutDataObject.MakeActorTransformAll(actorId, location, rotation, scale);
            NetworkManager.Instance.SendGameDataObject(gameOutObject);
        }
    }
}
