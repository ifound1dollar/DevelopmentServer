using ENetServer;
using ENetServer.NetObjects;
using ENetServer.NetObjects.DataObjects;
using ENetServer.Network;
using System.Diagnostics;
using static ENetServer.NetStatics;

namespace ServerExample
{
    internal class Program_Server
    {
        private static GameSimulatorWorker GameSimulator { get; set; } = new();

        static void Main(string[] args)
        {
            // Wrap entire main function in try-catch-finally to ensure ENet is deinitialized at exit.
            try
            {
                // FIRST, ask user for port.
                Console.Write("Enter port number to run server host on (minimum {0}): ", ServerPortMin);
                string? userInput = Console.ReadLine();
                bool validPort = false;
                ushort argPort = ServerPortMin;
                if (userInput != null)
                {
                    validPort = ushort.TryParse(userInput, out argPort);
                    if (argPort < ServerPortMin)
                    {
                        Console.WriteLine("Port number out of range, defaulting to {0}.",
                            ServerPortMin);
                        argPort = ServerPortMin;
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
                if (validPort)
                {
                    NetworkManager.Instance.SetupAsServer(argPort);
                }
                else
                {
                    NetworkManager.Instance.SetupAsServer(ServerPortMin);
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
                        ServerDisconnectAllRemoteHosts();
                    }
                    else if (inputLower == "tr")
                    {
                        ServerSendTransformToAll(uint.MaxValue, -6546515611561564564, 2, 3);
                    }
                    else if (inputSplit.Length > 0 && inputSplit[0] == "connect")
                    {
                        if (inputSplit.Length < 2) continue;

                        if (ushort.TryParse(inputSplit[1], out ushort port))
                        {
                            if ((validPort && port == argPort)) continue;   // Prevent connection to self.
                            ServerConnectToHost("127.0.0.1", port);
                        }
                    }
                    else if (inputLower == "gc")
                    {
                        ForceGarbageCollection();
                    }
                    else if (inputSplit.Length > 0 && inputSplit[0] == "stress")
                    {
                        if (inputSplit.Length < 2) continue;

                        if (int.TryParse(inputSplit[1], out int duration) && duration > 0 && duration <= 10)
                        {
                            GameSimulator.PauseThread();
                            RunGameObjectStressTest(duration);
                            GameSimulator.ResumeThread();
                        }
                    }
                    else if (inputSplit.Length > 0 && inputSplit[0] == "net")
                    {
                        if (inputSplit.Length < 2) continue;

                        if (uint.TryParse(inputSplit[1], out uint numObjects))
                        {
                            GameSimulator.PauseThread();
                            RunTotalSystemStressTest(numObjects);
                            GameSimulator.ResumeThread();
                        }
                    }
                    else
                    {
                        ServerBroadcastTextToAll(inputRaw);
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

                //Console.Write("\nPress any key to close console...");
                //Console.ReadKey();
            }
        }



        /// <summary>
        /// Instructs the server to attempt to connect to a remote host at the specified IP and port.
        /// </summary>
        /// <param name="ip"> IP address of host to connect to. </param>
        /// <param name="port"> Port of host to connect to. </param>
        public static void ServerConnectToHost(string ip, ushort port)
        {
            Console.WriteLine("[ACTION] Attempting to connect to remote host at {0}:{1}...", ip, port);

            // First, verify that we are not already connected to a host with this information.
            var temp = GameSimulator.Clients.ToArray();
            foreach (var host in temp)
            {
                if (host.Value.IP == ip && host.Value.Port == port)
                {
                    Console.WriteLine("[ERROR] Already connected to a host at {0}:{1}. Aborting.", ip, port);
                    return;
                }
            }

            GameSendObject gameSendObject = GameSendObject.Factory.CreateConnectOne(ip, port, 7777u);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Instructs the server to disconnect all connected clients on next Tick.
        /// </summary>
        public static void ServerDisconnectAllRemoteHosts()
        {
            Console.WriteLine("[ACTION] Disconnecting all remote hosts.");

            GameSendObject gameSendObject = GameSendObject.Factory.CreateDisconnectAll(HostType.Both, 200u);
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
                return;
            }

            GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageAll(HostType.Client, gameDataObject);
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
                return;
            }

            GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageAll(HostType.Client, gameDataObject);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Runs a stress test for the passed-in duration, creating new GameDataObjects continuously
        ///  until the duration ends.
        /// </summary>
        /// <param name="duration"> Duration in seconds to run the stress test. </param>
        public static void RunGameObjectStressTest(int duration)
        {
            Console.WriteLine("[ACTION] Running GameDataObject creation stress test for {0}s...",
                duration);

            int durationMS = duration * 1000;
            long counter = 0;

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < durationMS)
            {
                TESTDataObject.Factory.CreateFromDefault(counter.ToString());
                //TransformDataObject.Factory.CreateFromDefault(0, [0, 1, 2, 3, 4, 5, 6, 7, 8]);
                counter++;
            }

            sw.Stop();
            sw.Reset();

            Console.WriteLine("[LOG] Total number of GameDataObjects created during {0}s stress test: {1:n0}",
                duration, counter);
            Console.WriteLine("[LOG] Average per second: {0:n0}", counter / duration);
        }

        /// <summary>
        /// Runs a total-system stress test with a specified number of objects passed along the
        ///  networking system. Does not actually send messages, but simulates by re-queuing.
        /// </summary>
        /// <param name="numObjects"> Number of objects to pass through the networking system. </param>
        public static void RunTotalSystemStressTest(uint numObjects)
        {
            Console.WriteLine("[ACTION] Running total-system network stress test with {0:n0} total objects...",
                numObjects);

            uint sendCounter = 0, recvCounter = 0;
            Stopwatch sw = Stopwatch.StartNew();

            // Run stress test until BOTH send and receive have dequeued all.
            while (sendCounter < numObjects || recvCounter < numObjects)
            {
                // Send
                if (sendCounter < numObjects)
                {
                    GameDataObject? gameDataObject = TESTDataObject.Factory.CreateFromDefault(sendCounter.ToString());
                    if (gameDataObject != null)
                    {
                        GameSendObject gameSendObject = GameSendObject.Factory.CreateTestSend(false, sendCounter, gameDataObject);
                        NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
                        sendCounter++;

                        if (sendCounter >= numObjects)
                        {
                            Console.WriteLine("[LOG] Send operations completed after {0}ms.",
                                sw.ElapsedMilliseconds);
                        }
                    }
                }

                // Recv
                if (recvCounter < numObjects)
                {
                    // Using this while loop ensures that execution waits until the just-enqueued
                    //  GameSendObject returns as a GameRecvObject (keeps queues minimum size).
                    //while (NetworkManager.Instance.DequeueGameRecvObject() == null)
                    //{

                    //}
                    //recvCounter++;

                    // Using this if statement tries to dequeue, simply skipping if nothing to
                    //  dequeue. Enqueuing operations are executed faster, and this method works
                    //  as a means of seeing exactly how long it takes in total to complete
                    //  receive operations that do not cause send operations to wait.
                    if (NetworkManager.Instance.DequeueGameRecvObject() != null)
                    {
                        recvCounter++;
                    }

                    if (recvCounter >= numObjects)
                    {
                        Console.WriteLine("[LOG] Recv operations completed after {0}ms.",
                            sw.ElapsedMilliseconds);
                    }
                }
            }

            // Stop and reset stopwatch, and log total elapsed time.
            sw.Stop();
            Console.WriteLine("[LOG] Total elapsed time for network stress test with {0:n0} messages: {1}ms",
                numObjects, sw.ElapsedMilliseconds);
            Console.WriteLine("[LOG] Average per second: {0:n0}",
                numObjects / (sw.ElapsedMilliseconds / 1000.0f));
            sw.Reset();
        }

        /// <summary>
        /// Forces the Garbage Collector to collect ASAP.
        /// </summary>
        public static void ForceGarbageCollection()
        {
            Console.WriteLine("[ACTION] Running garbage collection.");

            // Set LOH compact mode which will compact it on next GC. Is reset to Default on collection.
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                            System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            System.GC.Collect();
        }
    }
}
