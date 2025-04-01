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
        private Connection? MasterServer { get; set; }
        private HashSet<uint> Checksums { get; } = [ 7777u, 8888u ];
        private HashSet<string> LoginTokens { get; } = [ "0f8fad5bd9cb469fa16570867728950e" ];

        private string TempClientLoginToken { get; } = "0f8fad5bd9cb469fa16570867728950e";

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
            // Validate new connection from MasterServer port.
            if (gameRecvObject.PeerParams.Port == 7776)
            {
                ProcessMasterServerConnect(gameRecvObject);
            }

            // Handle connection from other game server.
            if (gameRecvObject.PeerParams.HostType == HostType.Server)
            {
                ProcessGameServerConnect(gameRecvObject);
            }
            // Else if connection is from client.
            else if (gameRecvObject.PeerParams.HostType == HostType.Client)
            {
                ProcessClientConnect(gameRecvObject);
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
            }
            else if (peerParams.HostType == HostType.Client)
            {
                Console.WriteLine("[DISCONNECT] Client disconnected (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Clients.Remove(peerParams.ID, out _);
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
            }
            else if (peerParams.HostType == HostType.Client)
            {
                Console.WriteLine("[TIMEOUT] Server timed out (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                Clients.Remove(peerParams.ID, out _);
            }
        }

        private void HandleMessage(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            // If from master server, should contain login info (token and checksum).
            if (peerParams.Port == 7776 && MasterServer != null)
            {
                ProcessMasterServerMessage(gameRecvObject);
            }

            // If message received from server.
            if (peerParams.HostType == HostType.Server)
            {
                if (!Servers.TryGetValue(peerParams.ID, out Connection? connection)) return;

                ProcessGameServerMessage(gameRecvObject);
            }
            // Else handle message received from client.
            else if (peerParams.HostType == HostType.Client)
            {
                if (!Clients.TryGetValue(peerParams.ID, out Connection? connection)) return;

                // If connection is not validated, this message must contain login token.
                if (!connection.IsValidated)
                {
                    ProcessClientTokenVerification(gameRecvObject.GameDataObject, peerParams, connection);
                }
                else
                {
                    ProcessClientMessage(gameRecvObject);
                }
            }
        }

        #endregion

        #region Connect processing

        private void ProcessMasterServerConnect(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            if (MasterServer == null)
            {
                Console.WriteLine("[CONNECT] Successfully connected to master server (ID: {0}), Address {1}:{2}",
                gameRecvObject.PeerParams.ID,
                gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port);

                // Initialize MasterServer object with new Connection.
                MasterServer = new(peerParams.ID, peerParams.IP, peerParams.Port, true);
            }
            else
            {
                Console.WriteLine("[ERROR] Invalid new connection on port 7776 - MasterServer connection already exists.");

                // Data uint of 1006 indicates master server validation error.
                GameSendObject gameSendObject = GameSendObject.Factory.CreateDisconnectOne(peerParams.ID, 1006);
                NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
            }
        }

        private void ProcessGameServerConnect(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            Console.WriteLine("[CONNECT] New connection to game server (ID: {0}), Address: {1}:{2}, Data: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

            // Create new connection and add to Servers map.
            Connection connection = new(peerParams.ID, peerParams.IP, peerParams.Port, true);
            Servers[peerParams.ID] = connection;



            /* ----- BELOW CODE NEEDED FOR CLIENT ONLY ----- */

            // Client must send login token to new server connection immediately.
            GameDataObject? gameDataObject = TextDataObject.Factory.CreateFromDefault(TempClientLoginToken);
            if (gameDataObject != null)
            {
                GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageOne(peerParams.ID, gameDataObject);
                NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
            }
        }

        private void ProcessClientConnect(GameRecvObject gameRecvObject)
        {
            PeerParams peerParams = gameRecvObject.PeerParams;

            // If new connection passed valid checksum, add to Clients dict.
            if (Checksums.Contains(gameRecvObject.Data))
            {
                Console.WriteLine("[CONNECT] Client successfully connected (ID: {0}), Address: {1}:{2}, Checksum: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.Data);

                //Checksums.Remove(gameRecvObject.Data);

                // Create new Connection and add to Clients map.
                Connection connection = new(peerParams.ID, peerParams.IP, peerParams.Port, false);
                Clients[peerParams.ID] = connection;

                // Prompt client to send login token on next message.
                GameDataObject? textDataObject = TextDataObject.Factory.CreateFromDefault(
                    "Client must send full login token as next message.");
                if (textDataObject != null)
                {
                    GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageOne(connection.ID, textDataObject);
                    NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
                }
            }
            // Else force disconnect immediately.
            else
            {
                Console.WriteLine("[ERROR] Invalid new connection from {0}:{1} - checksum failed. Checksum: {2}",
                    peerParams.IP, peerParams.Port, gameRecvObject.Data);

                // Data uint of 1000 indicates client validation error.
                GameSendObject gameSendObject = GameSendObject.Factory.CreateDisconnectOne(peerParams.ID, 1000);
                NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
            }
        }

        #endregion

        #region Message processing

        private void ProcessMasterServerMessage(GameRecvObject gameRecvObject)
        {
            // READ LOGINDATAOBJECT AND ADD TO BOTH HASH SETS
        }

        private void ProcessGameServerMessage(GameRecvObject gameRecvObject)
        {
            Console.WriteLine("[MESSAGE] Message received from server (ID: {0}), Address: {1}:{2}, Message: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.GameDataObject?.GetDescription());
        }

        private void ProcessClientMessage(GameRecvObject gameRecvObject)
        {
            Console.WriteLine("[MESSAGE] Message received from client (ID: {0}), Address: {1}:{2}, Message: {3}",
                    gameRecvObject.PeerParams.ID,
                    gameRecvObject.PeerParams.IP, gameRecvObject.PeerParams.Port,
                    gameRecvObject.GameDataObject?.GetDescription());
        }

        private void ProcessClientTokenVerification(GameDataObject? gameDataObject, PeerParams peerParams, Connection connection)
        {
            bool success = false;
            if (gameDataObject != null && gameDataObject.DataType == DataType.Text)
            {
                // Safe to hard cast here because DataType is guaranteed to match actual derived object type.
                TextDataObject textDataObject = (TextDataObject)gameDataObject;

                // Set success to true if successfully removed (existed in map).
                if (/*LoginTokens.Remove(textDataObject.String*/LoginTokens.Contains(textDataObject.String))
                {
                    success = true;
                }
            }

            // Validate client and send ACK if successful, else log failure and disconnect client.
            if (success)
            {
                Console.WriteLine("[CONNECT] Client passed login validation check (ID: {0})",
                    peerParams.ID);
                connection.Validate();

                // Send message to client informing it that it is now fully connected.
                GameDataObject? textDataObject = TextDataObject.Factory.CreateFromDefault(
                    "Successfully validated connection.");
                if (textDataObject != null)
                {
                    GameSendObject gameSendObject = GameSendObject.Factory.CreateMessageOne(connection.ID, textDataObject);
                    NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
                }
            }
            else
            {
                Console.WriteLine("[DISCONNECT] Client failed validation check, disconnecting (ID: {0})",
                    peerParams.ID);

                // Remove clients from map and disconnect this client, data uint 1000u means validation error.
                Clients.Remove(peerParams.ID, out _);
                GameSendObject gameSendObject = GameSendObject.Factory.CreateDisconnectOne(peerParams.ID, 1000u);
                NetworkManager.Instance.EnqueueGameSendObject(gameSendObject);
            }
        }

        #endregion
    }
}
