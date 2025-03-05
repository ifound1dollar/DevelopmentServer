using ENet;
using ENetServer.NetObjects;
using ENetServer.NetObjects.DataObjects;
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
            NetworkManager.Instance.SetupAsServer();
            //NetworkManager.Instance.SetupAsClient();
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
                    ServerSendTransformToAll(uint.MaxValue, -6546515611561564564, 2, 3);
                }
                else
                {
                    ServerBroadcastTextToAll(input);
                }
            }

            // After loop exits, stop the worker thread (blocks here).
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

            GameSendObject gameSendObject = new GameSendObject.Builder()
                .ForDisconnectAll()
                .Build();
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Instructs the server to broadcast the passed-in string to all connected clients.
        /// </summary>
        /// <param name="message"> The message string to broadcast. </param>
        public static void ServerBroadcastTextToAll(string message)
        {
            Console.WriteLine("[ACTION] Broadcasting message \"" + message + "\" to all clients.");

            GameDataObject gameDataObject = new TextDataObject.Builder()
                .FromString(message)
                .Build();
            GameSendObject gameSendObject = new GameSendObject.Builder()
                .ForMessageAll(gameDataObject)
                .Build();
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Instructs the server to send a transform object to all connected clients.
        /// </summary>
        /// <param name="actorId"> ID of Actor corresponding to the transform. </param>
        /// <param name="location"> Location component of the transform. </param>
        /// <param name="rotation"> Rotation component of the transform. </param>
        /// <param name="scale"> Scale component of the transform. </param>
        public static void ServerSendTransformToAll(uint actorId, double locX, double locY, double locZ)
        {
            Console.WriteLine("[ACTION] Broadcasting location-only transform {0}, {1}, {2} for actor with ID {3} to all clients.",
                locX, locY, locZ, actorId);

            // Build GameDataObject, then build GameSendObject and enqueue.
            TransformDataObject transformDataObject = new TransformDataObject.Builder()
                .FromLocation(actorId, locX, locY, locZ)
                .Build();
            GameSendObject gameSendObject = new GameSendObject.Builder()
                .ForMessageAll(transformDataObject)
                .Build();
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }
    }
}
