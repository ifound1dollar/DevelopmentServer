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
using System.Diagnostics.Metrics;
using System.ComponentModel;
using ENetServer.Network.Data;

namespace ENetServer
{
    public class GameSimulatorWorker
    {
        private readonly Thread thread;
        private volatile bool shouldExit = false;
        private volatile bool isPaused = false;
        private volatile bool isTesting = false;

        /// <summary>
        /// Map containing references to all Clients in format ID:Connection.
        /// </summary>
        public ConcurrentDictionary<uint, Connection> Clients { get; } = new();
        public ConcurrentDictionary<uint, Connection> Servers { get; } = new();
        public ConcurrentDictionary<uint, Connection> Connected { get; } = new();


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

            GameDataObject? gameDataObject = TESTDataObject.Factory.CreateFromDefault(counter.ToString());
            if (gameDataObject == null) return;

            // Send to server with ID 0.
            GameSendObject gameSendObject = GameSendObject.Factory.CreateTestSend(true, 0u, gameDataObject);
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
                    // DO NOTHING WITH DEFAULT CASE
                }

                /* - outside of switch case, inside foreach loop - */
            }
        }





        #region Receive Handlers

        private void HandleConnect(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            if (peerParams.HostType == HostType.Server)
            {
                Console.WriteLine("[CONNECT] Connected to server (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Connection connection = new(peerParams.ID, peerParams.IP, peerParams.Port, true);
                Servers[peerParams.ID] = connection;
                Connected[peerParams.ID] = connection;
            }
            else if (peerParams.HostType == HostType.Client)
            {
                Console.WriteLine("[CONNECT] Client connected (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Connection connection = new(peerParams.ID, peerParams.IP, peerParams.Port, false);
                Clients[peerParams.ID] = connection;
                Connected[peerParams.ID] = connection;
            }
        }

        private void HandleDisconnect(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            if (peerParams.HostType == HostType.Server)
            {
                Console.WriteLine("[DISCONNECT] Disconnected from server (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Servers.Remove(peerParams.ID, out _);
                Connected.Remove(peerParams.ID, out _);
            }
            else if (peerParams.HostType == HostType.Client)
            {
                Console.WriteLine("[DISCONNECT] Client disconnected (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Clients.Remove(peerParams.ID, out _);
                Connected.Remove(peerParams.ID, out _);
            }
        }

        private void HandleTimeout(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            if (peerParams.HostType == HostType.Server)
            {
                Console.WriteLine("[TIMEOUT] Timed out from server (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Servers.Remove(peerParams.ID, out _);
                Connected.Remove(peerParams.ID, out _);
            }
            else if (peerParams.HostType == HostType.Client)
            {
                Console.WriteLine("[TIMEOUT] Client timed out (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Clients.Remove(peerParams.ID, out _);
                Connected.Remove(peerParams.ID, out _);
            }
        }

        private void HandleMessage(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            if (peerParams.HostType == HostType.Server)
            {
                Console.WriteLine("[MESSAGE] Server message (ID: {0}), Address: {1}:{2}, Message: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.GameDataObject?.GetDescription());
            }
            else if (peerParams.HostType == HostType.Client)
            {
                Console.WriteLine("[MESSAGE] Client message (ID: {0}), Address: {1}:{2}, Message: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.GameDataObject?.GetDescription());
            }
        }

        #endregion
    }
}
