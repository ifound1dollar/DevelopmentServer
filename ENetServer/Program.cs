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
            // Wrap entire main function in try-catch-finally to ensure ENet is deinitialized at exit.
            try
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
                string? inputRaw;
                string? inputLower;
                while (true)
                {
                    Console.Write("> ");
                    inputRaw = Console.ReadLine();
                    if (inputRaw == null) break;
                    inputLower = inputRaw.ToLower();

                    // Operate on input here, separate thread from server thread.
                    if (inputLower == "exit" || inputLower == "e")
                    {
                        break;
                    }
                    else if (inputLower == "dc")
                    {
                        ServerDisconnectAllClients();
                    }
                    else if (inputLower == "tr")
                    {
                        ServerSendTransformToAll(uint.MaxValue, -6546515611561564564, 2, 3);
                    }
                    else
                    {
                        ServerBroadcastTextToAll(inputRaw);
                    }

                    // Thread.Sleep() here for a short time to allow operations to complete before allowing new input.
                    Thread.Sleep(100);
                }

                // After loop exits, stop the worker thread (blocks here).
                NetworkManager.Instance.StopThreadedOperations();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[EXCEPTION] :: {e}");
            }
            finally // Is always called unless there are unique extenuating circumstances.
            {
                // Always de-initialize ENet library on application exit.
                ENet.Library.Deinitialize();

                // Log de-initialization.
                Console.WriteLine("[EXIT] De-initialized ENet library.");
            }
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

            GameDataObject? gameDataObject = TextDataObject.Factory.CreateFromDefault(message);
            if (gameDataObject == null)
            {
                Console.WriteLine("[ERROR] Failed to create TextDataObject. Aborting.");
            }

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
            Console.WriteLine("[ACTION] Broadcasting location [{0}, {1}, {2}] for actor with ID {3} to all clients.",
                locX, locY, locZ, actorId);

            // Build GameDataObject, then build GameSendObject and enqueue.
            GameDataObject? gameDataObject = TransformDataObject.Factory.CreateFromDefault(actorId,
                [locX, locY, locZ, 0.0d, 0.0d, 0.0d, 1.0d, 1.0d, 1.0d]);
            if (gameDataObject == null)
            {
                Console.WriteLine("[ERROR] Failed to create TransformDataObject. Aborting.");
            }

            GameSendObject gameSendObject = new GameSendObject.Builder()
                .ForMessageAll(gameDataObject)
                .Build();
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }
    }
}
