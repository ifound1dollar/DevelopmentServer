using ENetServer.Network;
using ENetServer.NetObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;
using System.Collections.Concurrent;
using ENetServer.NetObjects.DataObjects;

namespace ENetServer
{
    public class GameSimulatorWorker
    {
        private readonly Thread thread;
        private volatile bool shouldExit = false;
        private volatile bool isPaused = false;
        private volatile bool isTesting = false;

        /// <summary>
        /// Map containing references to all Connections (both client and server) in format ID:Connection.
        /// </summary>
        public ConcurrentDictionary<uint, Connection> Connections { get; } = new();

        public GameSimulatorWorker()
        {
            thread = new(DoFixedIntervalTick);
        }



        /// <summary>
        /// Starts the worker thread, beginning server operations on a separate thread.
        /// </summary>
        public void StartThread()
        {
            thread.Start();
            Console.WriteLine("[STARTUP] GameSimulator thread started.");
        }

        /// <summary>
        /// Stops the worker thread, waiting for server to shut down before joining and returning.
        /// </summary>
        public void StopThread()
        {
            // Sets shouldExit to true, which will gracefully exit the threaded loop.
            shouldExit = true;

            // Wait for the Run() function to return, then join the thread (BLOCKS).
            Console.WriteLine("[EXIT] Waiting for GameSimulator to stop...");
            thread.Join();
            Console.WriteLine("[EXIT] GameSimulator stopped successfully.");
        }

        /// <summary>
        /// Sets a volatile boolean that will pause threaded Tick operations until resumed.
        /// </summary>
        public void PauseThread()
        {
            isPaused = true;
        }

        /// <summary>
        /// Sets a volatile boolean that will resume threaded Tick operations if paused.
        /// </summary>
        public void ResumeThread()
        {
            isPaused = false;
        }

        public void StartTesting()
        {
            isTesting = true;
        }

        public void StopTesting()
        {
            isTesting = false;
        }

        public bool GetIsTesting()
        {
            return isTesting;
        }


        private void DoFixedIntervalTick()
        {
            double tickIntervalExact = 1000.0d / 10.0d;     // Second double is TPS (divide ms value)
            int tickInterval = (int)Math.Round(tickIntervalExact);
            int sleepTime;
            Stopwatch stopwatch = new();

            //TEMP
            long counter = 0;
            //TEMP

            // Will continue looping until 'shouldExit' is set to true, which should be done via SetShouldExit().
            while (!shouldExit)
            {
                if (isPaused) continue;

                // Restart timer and actually perform tick operations.
                stopwatch.Restart();

                TickSend(counter++);

                TickReceive();



                /* ----- BELOW: WAIT FOR FIXED DURATION UNTIL NEXT TICK ----- */

                // Sleep method execution takes on average another ~1ms, so sleep for 2ms less.
                // Then, block for remaining duration (blocking causes high CPU utilization).
                sleepTime = tickInterval - (int)stopwatch.ElapsedMilliseconds - 2;

                // Only sleep if wait time is more than 2ms (not worth it otherwise).
                if (sleepTime > 2)
                {
                    Thread.Sleep(sleepTime);
                }

                // Manual block for remaining exact milliseconds (high CPU utilization).
                while (stopwatch.Elapsed.TotalMilliseconds < tickIntervalExact)
                {
                    // Block
                }

                //TEMP
                //Console.WriteLine(stopwatch.Elapsed.TotalMilliseconds.ToString());
                //TEMP
            }
        }

        private void TickSend(long counter)
        {
            // Do nothing while not testing (TEMP).
            if (!isTesting) return;

            GameDataObject? gameDataObject = TextDataObject.Factory.CreateFromDefault(counter.ToString());
            if (gameDataObject == null) return;

            // Send to server with ID 0.
            GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageOne(0u, gameDataObject);
            NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
        }

        private void TickReceive()
        {
            // Dequeue and get all GameRecvObjects in the queue at the start of the tick.
            GameRecvObject?[] gameRecvObjects = NetworkManager.Instance.DequeueAllGameRecvObjects();

            // Do not print output while testing.
            if (isTesting)
            {
                return;
            }

            // Actually handle each dequeued GameRecvObject, skipping if null.
            foreach (var gameRecvObject in gameRecvObjects)
            {
                if (gameRecvObject == null) continue;

                // Operate based on receive type.
                switch (gameRecvObject.RecvType)
                {
                    case RecvType.Connect:
                        {
                            HandleConnect(gameRecvObject);
                            break;
                        }
                    case RecvType.Disconnect:
                        {
                            HandleDisconnect(gameRecvObject);
                            break;
                        }
                    case RecvType.Timeout:
                        {
                            HandleTimeout(gameRecvObject);
                            break;
                        }
                    case RecvType.Message:
                        {
                            HandleMessage(gameRecvObject);
                            break;
                        }
                    case RecvType.TestRecv: // THIS CASE SHOULD NEVER BE REACHED, GAMESIMULATOR SHOULD BE PAUSED DURING TEST
                        {
                            Console.WriteLine("[TEST] TestRecv object received successfully.");
                            break;
                        }
                    // DO NOTHING WITH DEFAULT CASE
                }

                /* - outside of switch case, inside foreach loop - */
            }
        }



        #region Receive Handlers

        private void HandleConnect(GameRecvObject gameRecvObject)
        {
            // Different message handling based on client or server.
            if (NetworkManager.Instance.IsServer)
            {
                string temp = (gameRecvObject.Connection.IsServer) ? "Server" : "Client";
                Console.WriteLine("[CONNECT] {0} connected (ID: {1}), Address: {2}:{3}",
                    temp, gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port);
            }
            else
            {
                Console.WriteLine("[CONNECT] Successfully connected to server (ID: {0}), Address {1}:{2}",
                    gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port);
            }

            // Try to add new Connection to map.
            Connections.TryAdd(gameRecvObject.Connection.ID, gameRecvObject.Connection);
        }

        private void HandleDisconnect(GameRecvObject gameRecvObject)
        {
            // Different message handling based on client or server.
            if (NetworkManager.Instance.IsServer)
            {
                string temp = (gameRecvObject.Connection.IsServer) ? "Server" : "Client";
                Console.WriteLine("[DISCONNECT] {0} disconnected (ID: {1}), Address: {2}:{3}",
                    temp, gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port);
            }
            else
            {
                Console.WriteLine("[DISCONNECT] Disconnected from server (ID: {0}), Address {1}:{2}",
                    gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port);
            }

            // Remove now-disconnected Connection from map.
            Connections.Remove(gameRecvObject.Connection.ID, out _);
        }

        private void HandleTimeout(GameRecvObject gameRecvObject)
        {
            // Different message handling based on client or server.
            if (NetworkManager.Instance.IsServer)
            {
                string temp = (gameRecvObject.Connection.IsServer) ? "Server" : "Client";
                Console.WriteLine("[TIMEOUT] {0} timed out (ID: {1}), Address: {2}:{3}",
                    temp, gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port);
            }
            else
            {
                Console.WriteLine("[TIMEOUT] Timed out from server (ID: {0}), Address {1}:{2}",
                    gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port);
            }

            // Remove now-disconnected Connection from map.
            Connections.Remove(gameRecvObject.Connection.ID, out _);
        }

        private void HandleMessage(GameRecvObject gameRecvObject)
        {
            // Different message handling based on client or server.
            if (NetworkManager.Instance.IsServer)
            {
                string temp = (gameRecvObject.Connection.IsServer) ? "server" : "client";
                Console.WriteLine("[MESSAGE] Message received from {0} (ID: {1}), Address: {2}:{3} Message: {4}",
                    temp, gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port,
                    gameRecvObject.GameDataObject?.GetDescription());
            }
            else
            {
                Console.WriteLine("[MESSAGE] Message received from server (ID: {0}), Address: {1}:{2} Message: {3}",
                    gameRecvObject.Connection.ID,
                    gameRecvObject.Connection.IP, gameRecvObject.Connection.Port,
                    gameRecvObject.GameDataObject?.GetDescription());
            }
        }

        #endregion
    }
}
