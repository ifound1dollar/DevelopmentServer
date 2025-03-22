using ENetServer.Management;
using ENetServer.NetObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer
{
    public class GameSimulatorWorker
    {
        private readonly Thread thread;
        private volatile bool shouldExit = false;
        private volatile bool isPaused = false;

        public GameSimulatorWorker()
        {
            thread = new(DoFixedIntervalTick);
        }



        /// <summary>
        /// Starts the worker thread, beginning server operations on a separate thread.
        /// </summary>
        internal void StartThread()
        {
            thread.Start();
            Console.WriteLine("[STARTUP] GameSimulator thread started.");
        }

        internal void PauseThread()
        {
            isPaused = true;
        }

        internal void ResumeThread()
        {
            isPaused = false;
        }

        /// <summary>
        /// Stops the worker thread, waiting for server to shut down before joining and returning.
        /// </summary>
        internal void StopThread()
        {
            // Sets shouldExit to true, which will gracefully exit the threaded loop.
            shouldExit = true;

            // Wait for the Run() function to return, then join the thread (BLOCKS).
            Console.WriteLine("[EXIT] Waiting for GameSimulator to stop...");
            thread.Join();
            Console.WriteLine("[EXIT] GameSimulator stopped successfully.");
        }

        private void DoFixedIntervalTick()
        {
            double tickIntervalExact = 1000.0d / 30.0d;     // 30 per second (33.3ms/tick)
            int tickInterval = (int)Math.Round(tickIntervalExact);
            int sleepTime;
            Stopwatch stopwatch = new();

            // Will continue looping until 'shouldExit' is set to true, which should be done via SetShouldExit().
            while (!shouldExit)
            {
                if (isPaused) continue;

                // Restart timer and actually perform tick operations.
                stopwatch.Restart();
                TickService();



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

        private void TickService()
        {
            // Dequeue and get all GameRecvObjects in the queue at the start of the tick.
            GameRecvObject?[] gameRecvObjects = NetworkManager.Instance.DequeueAllGameRecvObjects();

            // Actually handle each dequeued GameRecvObject.
            foreach (var gameRecvObject in gameRecvObjects)
            {
                // Skip if null (should never be).
                if (gameRecvObject == null) continue;

                // Operate based on receive type.
                switch (gameRecvObject.RecvType)
                {
                    case RecvType.Connect:
                        {
                            Console.WriteLine("[CONNECT] Client connected - ID: " + gameRecvObject.Connection.ID +
                                ", Address: " + gameRecvObject.Connection.IP + ":" + gameRecvObject.Connection.Port);
                            break;
                        }
                    case RecvType.Disconnect:
                        {
                            Console.WriteLine("[DISCONNECT] Client disconnected - ID: " + gameRecvObject.Connection.ID +
                                ", Address: " + gameRecvObject.Connection.IP + ":" + gameRecvObject.Connection.Port);
                            break;
                        }
                    case RecvType.Timeout:
                        {
                            Console.WriteLine("[TIMEOUT] Client timed out - ID: " + gameRecvObject.Connection.ID +
                                ", Address: " + gameRecvObject.Connection.IP + ":" + gameRecvObject.Connection.Port);
                            break;
                        }
                    case RecvType.Message:
                        {
                            Console.WriteLine("[MESSAGE] Packet received from - ID: " + gameRecvObject.Connection.ID +
                                ", Message: " + gameRecvObject.GameDataObject?.GetDescription());
                            break;
                        }
                    case RecvType.TestRecv:
                        {
                            Console.WriteLine("[TEST] TestRecv object received successfully.");
                            break;
                        }
                    // DO NOTHING WITH DEFAULT CASE
                }

                // If GameDataObject is not null, return it to its object pool.
                if (gameRecvObject.GameDataObject != null)
                {
                    gameRecvObject.GameDataObject.ReturnToPool();
                }
            }
        }
    }
}
