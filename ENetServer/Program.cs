﻿using ENet;
using ENetServer.NetObjects;
using ENetServer.NetObjects.DataObjects;
using System.Diagnostics;
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
                string[]? inputSplit;
                while (true)
                {
                    Console.Write("> ");
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
                        ServerDisconnectAllClients();
                    }
                    else if (inputLower == "tr")
                    {
                        ServerSendTransformToAll(uint.MaxValue, -6546515611561564564, 2, 3);
                    }
                    else if (inputSplit.Length > 0 && inputSplit[0] == "stress")
                    {
                        if (inputSplit.Length < 2) continue;

                        if (int.TryParse(inputSplit[1], out int duration) && duration > 0 && duration < 10)
                        {
                            RunGameObjectStressTest(duration);
                        }
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

            GameSendObject gameSendObject = GameSendObject.Factory.CreateDisconnectAll();
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

            GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageAll(gameDataObject);
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

            GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageAll(gameDataObject);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        /// <summary>
        /// Runs a stress test for the passed-in duration, creating new GameDataObjects continuously
        ///  until the duration ends.
        /// </summary>
        /// <param name="duration"> Duration in seconds to run the stress test. </param>
        public static void RunGameObjectStressTest(int duration)
        {
            int durationMS = duration * 1000;
            long counter = 0;

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < durationMS)
            {
                TextDataObject.Factory.CreateFromDefault("test");
                //TransformDataObject.Factory.CreateFromDefault(0, [0, 1, 2, 3, 4, 5, 6, 7, 8]);
                counter++;
            }

            sw.Stop();
            sw.Reset();
            
            Console.WriteLine("[LOG] Total number of GameDataObjects created during {0}s stress test: {1:n0}", duration, counter);
            Console.WriteLine("[LOG] Average per second: {0:n0}", counter / duration);
        }
    }
}
