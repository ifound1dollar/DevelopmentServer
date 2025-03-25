using ENetServer;
using ENetServer.NetObjects;
using ENetServer.NetObjects.DataObjects;
using ENetServer.Network;
using System.Diagnostics;
using static ENetServer.NetStatics;

namespace ClientExample
{
    internal class Program_Client
    {
        private static GameSimulatorWorker GameSimulator { get; set; } = new();

        static void Main(string[] args)
        {
            // Wrap entire main function in try-catch-finally to ensure ENet is deinitialized at exit.
            try
            {
                // FIRST, ask user for port.
                Console.Write("Enter port number to run client host on (minimum {0}): ", ClientPortMin);
                string? userInput = Console.ReadLine();
                bool validPort = false;
                ushort argPort = ClientPortMin;
                if (userInput != null)
                {
                    validPort = ushort.TryParse(userInput, out argPort);
                    if (argPort < ClientPortMin)
                    {
                        Console.WriteLine("Port number out of range, defaulting to 8888.");
                        argPort = ClientPortMin;
                    }
                }

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
                // Setup NetworkManager and start networking threads.
                if (validPort)
                {
                    NetworkManager.Instance.SetupAsClient(argPort);
                }
                else
                {
                    NetworkManager.Instance.SetupAsClient(ClientPortMin);
                }
                NetworkManager.Instance.StartThreadedOperations();

                // Setup and run GameSimulator.
                GameSimulator.StartThread();

                // Main thread input loop.
                string? inputRaw;
                string? inputLower;
                string[]? inputSplit;
                while (true)
                {
                    //Console.Write("> ");
                    inputRaw = Console.ReadLine();
                    if (inputRaw == null) break;
                    inputLower = inputRaw.ToLower();
                    inputSplit = inputLower.Split(' ');

                    // Operate on input here, separate thread from server thread.
                    if (inputLower == "exit" || inputLower == "e" || inputLower == "stop" || inputLower == "st")
                    {
                        break;
                    }
                    else if (inputLower == "dc")
                    {
                        ClientDisconnectFromAll();
                    }
                    else if (inputLower == "test")
                    {
                        ClientToggleGameSimulatorTest();
                    }
                    else if (inputSplit.Length > 0 && inputSplit[0] == "dc")
                    {
                        if (inputSplit.Length < 2) continue;

                        if (uint.TryParse(inputSplit[1], out uint peerId))
                        {
                            ClientDisconnectFromOne(peerId);
                        }
                    }
                    else if (inputLower == "tr")
                    {
                        ClientSendTransformToAll(uint.MaxValue, -6546515611561564564, 2, 3);
                    }
                    else if (inputSplit.Length > 0 && inputSplit[0] == "connect")
                    {
                        if (inputSplit.Length < 2) continue;

                        if (ushort.TryParse(inputSplit[1], out ushort port))
                        {
                            if ((validPort && port == argPort) || port == 8888) continue;
                            ClientConnectToServer("127.0.0.1", port);
                        }
                    }
                    else
                    {
                        ClientBroadcastTextToAll(inputRaw);
                    }

                    // Thread.Sleep() here for a short time to allow operations to complete before allowing new input.
                    Thread.Sleep(100);
                }

                // After loop exits, stop the NetworkManager threaded operations (blocks here).
                NetworkManager.Instance.StopThreadedOperations();

                // Also stop GameSimulator.
                GameSimulator.StopThread();
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

                Console.Write("\nPress any key to close console...");
                Console.ReadKey();
            }
        }



        /// <summary>
        /// Instructs the client to attempt to connect to a remote host (server) at the specified IP and port.
        /// </summary>
        /// <param name="ip"> IP address of server to connect to. </param>
        /// <param name="port"> Port of server to connect to. </param>
        public static void ClientConnectToServer(string ip, ushort port)
        {
            Console.WriteLine("[ACTION] Attempting to connect to remote host at {0}:{1}...", ip, port);

            // First, verify that we are not already connected to a host with this information.
            var temp = GameSimulator.Connections.ToArray();
            foreach (var client in temp)
            {
                if (client.Value.IP == ip && client.Value.Port == port)
                {
                    Console.WriteLine("[ERROR] Already connected to a server at {0}:{1}. Aborting.", ip, port);
                    return;
                }
            }

            GameSendObject gameSendObject = GameSendObject.Factory.CreateConnectOne(ip, port);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Instructs the client to attempt to disconnect from a remote host by ID.
        /// </summary>
        /// <param name="peerId"> ID of remote host to attempt to disconnect from. </param>
        public static void ClientDisconnectFromOne(uint peerId)
        {
            Console.WriteLine("[ACTION] Attempting to disconnect from server with ID {0}...", peerId);

            // Try to find a Connection object for this peer ID, enqueuing if found.
            if (GameSimulator.Connections.TryGetValue(peerId, out var connection))
            {
                GameSendObject gameSendObject = GameSendObject.Factory.CreateDisconnectOne(connection.ID);
                NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
            }
            else
            {
                Console.WriteLine("[ERROR] Could not find server with ID {0}", peerId);
            }
        }

        /// <summary>
        /// Instructs the client to disconnect from all connected servers next tick.
        /// </summary>
        public static void ClientDisconnectFromAll()
        {
            Console.WriteLine("[ACTION] Disconnecting from all remote hosts.");

            GameSendObject gameSendObject = GameSendObject.Factory.CreateDisconnectAll(HostType.Both);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Instructs the client to broadcast the passed-in string to all connected servers.
        /// </summary>
        /// <param name="message"> The message string to broadcast. </param>
        public static void ClientBroadcastTextToAll(string message)
        {
            Console.WriteLine("[ACTION] Broadcasting message \"" + message + "\" to all servers.");

            GameDataObject? gameDataObject = TextDataObject.Factory.CreateFromDefault(message);
            if (gameDataObject == null)
            {
                Console.WriteLine("[ERROR] Failed to create TextDataObject. Aborting.");
                return;
            }

            GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageAll(HostType.Server, gameDataObject);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Instructs the client to send a transform object to all connected servers.
        /// </summary>
        /// <param name="actorId"> ID of Actor corresponding to the transform. </param>
        /// <param name="location"> Location component of the transform. </param>
        /// <param name="rotation"> Rotation component of the transform. </param>
        /// <param name="scale"> Scale component of the transform. </param>
        public static void ClientSendTransformToAll(uint actorId, double locX, double locY, double locZ)
        {
            Console.WriteLine("[ACTION] Broadcasting location [{0}, {1}, {2}] for actor with ID {3} to all servers.",
                locX, locY, locZ, actorId);

            // Build GameDataObject, then build GameSendObject and enqueue.
            GameDataObject? gameDataObject = TransformDataObject.Factory.CreateFromDefault(actorId,
                [locX, locY, locZ, 0.0d, 0.0d, 0.0d, 1.0d, 1.0d, 1.0d]);
            if (gameDataObject == null)
            {
                Console.WriteLine("[ERROR] Failed to create TransformDataObject. Aborting.");
                return;
            }

            GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageAll(HostType.Server, gameDataObject);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Toggles testing mode for the GameSimulator, which sends an object over the network
        ///  once per tick.
        /// </summary>
        public static void ClientToggleGameSimulatorTest()
        {
            if (!GameSimulator.GetIsTesting())
            {
                Console.WriteLine("[ACTION] Toggling on GameSimulator test mode - receive logging disabled while active.");
                GameSimulator.StartTesting();
            }
            else
            {
                Console.WriteLine("[ACTION] Toggling off GameSimulator test mode.");
                GameSimulator.StopTesting();
            }
        }
    }
}
