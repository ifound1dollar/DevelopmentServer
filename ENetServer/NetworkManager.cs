using ENet;
using ENetServer.NetObjects;
using ENetServer.Network;
using ENetServer.Serialize;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace ENetServer
{
    /// <summary>
    /// Singleton class that manages all ENet networking operations and serialization/deserialization. Multi-threaded.
    /// </summary>
    public class NetworkManager
    {
        // ----- DESCRIPTION ----- //
        // The NetworkManager class is a singleton that manages a multi-threaded networking system. This system
        //  manages two separate threads, one for serialization/deserialization and one for networking, which
        //  work concurrently to dramatically increase the efficiency of networking tasks.
        // The main thread should access this class Instance to enqueue and dequeue game data, never
        //  directly interacting with either of the running threads. This class strictly prohibits access to
        //  the running threads, ensuring thread safety and simplicity for developers. 
        // The Worker nested classes manage their own specific object instances that perform relevant tasks.
        //  The Serializer class handles all serialization/deserialization and communication. The Server and
        //  Client classes start, stop, and run the ENet host and handle network sending and receiving.

        // The main thread needs only to call the SetupAsClient()/SetupAsServer(), StartThreadedOperations(),
        //  and StopThreadedOperations() methods to manage this class. These methods fully encapsulate all
        //  networking operations.
        // Utilizing the NetworkManager only requires using EnqueueGameSendObject(), DequeueGameRecvObject(),
        //  and GetConnectedClient() (server only) to work with outgoing/incoming network data and with
        //  connected clients. The main/game thread can focus on working with the game data passed to /
        //  received from this class rather than handling the actual networking tasks (focus on gameplay and
        //  behavior, not networking).
        // ----- END DESCRIPTION ----- //

        public enum State { Uninitialized, Initialized, Running, Stopped }

        #region Singleton Stuff

        private static readonly NetworkManager instance = new();

        static NetworkManager()
        {
            // Static constructor allows for thread-safe singleton usage.
            // See: https://csharpindepth.com/articles/singleton, fourth option.
        }
        private NetworkManager()
        {
            // Default constructor
        }

        public static NetworkManager Instance { get { return instance; } }

        #endregion



        // Thread-safe queues for communicating game data between main and serialization/intermediate threads.
        private ConcurrentQueue<GameSendObject> GameSendQueue { get; } = new();
        private ConcurrentQueue<GameRecvObject> GameRecvQueue { get; } = new();

        // Thread-safe queues for communicating network data between network and serialization/intermediate threads.
        private ConcurrentQueue<NetSendObject> NetSendQueue { get; } = new();
        private ConcurrentQueue<NetRecvObject> NetRecvQueue { get; } = new();

        // Publicly accessible, these are readonly (in practice) so thread-safe.
        public bool IsServer { get; private set; }
        public ushort ServerPortMin { get; } = 7777;
        public ushort ClientPortMin { get; } = 8888;

        // These are nullable so they can be manually initialized in Setup().
        private SerializeWorker? serializeWorker;
        private ServerWorker? serverWorker;
        private ClientWorker? clientWorker;

        private State state;



        /// <summary>
        /// Sets up NetworkManager as server, initializing and configuring thread workers.
        /// </summary>
        public void SetupAsServer(ushort port = 7777)
        {
            // Throw exception if NetworkManager has already been initialized.
            if (state != State.Uninitialized)
            {
                string error = IsServer ? "Server." : "Client.";
                throw new InvalidOperationException("NetworkManager already initialized as " + error);
            }

            IsServer = true;
            serializeWorker = new SerializeWorker();
            serverWorker = new ServerWorker(port);

            state = State.Initialized;
        }

        /// <summary>
        /// Sets up NetworkManager as client, initializing and configuring thread workers.
        /// </summary>
        public void SetupAsClient(ushort port = 8888)
        {
            // Throw exception if NetworkManager has already been initialized.
            if (state != State.Uninitialized)
            {
                string error = IsServer ? "Server." : "Client.";
                throw new InvalidOperationException("NetworkManager already initialized as " + error);
            }

            IsServer = false;
            serializeWorker = new SerializeWorker();
            clientWorker = new ClientWorker(port);

            state = State.Initialized;
        }

        /// <summary>
        /// Starts separate threads for serialization/intermediate work and networking work.
        /// </summary>
        public void StartThreadedOperations()
        {
            // Verify proper state.
            if (state == State.Uninitialized)
            {
                throw new InvalidOperationException("Cannot start threads before NetworkManager is properly initialized. State: "
                    + state.ToString());
            }
            else if (state == State.Running)
            {
                throw new InvalidOperationException("Threaded operations are already running. State: " + state.ToString());
            }

            // Starts each threaded operation (one for serialization, one for ENet) here.
            if (IsServer)
            {
                serializeWorker?.StartThread();
                serverWorker?.StartThread();
            }
            else
            {
                serializeWorker?.StartThread();
                clientWorker?.StartThread();
            }

            state = State.Running;
        }

        /// <summary>
        /// Stops serialization/intermediate and network threads.
        /// </summary>
        public void StopThreadedOperations()
        {
            // If state is not Running, cannot stop threads.
            if (state != State.Running)
            {
                throw new InvalidOperationException("Cannot stop threads which are not running. State: " + state.ToString());
            }

            // Stops each threaded operation gracefully.
            if (IsServer)
            {
                serverWorker?.StopThread();
                serializeWorker?.StopThread();
            }
            else
            {
                clientWorker?.StopThread();
                serializeWorker?.StopThread();
            }

            // NOTE: NetworkWorker should be stopped first because disconnecting clients will enqueue
            //  disconnect events that should be handled by the serialization thread.

            state = State.Stopped;
        }



        public void EnqueueGameSendObject(GameSendObject gameSendObject)
        {
            // Queues the passed-in GameSendObject to be processed and sent.
            GameSendQueue.Enqueue(gameSendObject);
        }

        public GameRecvObject? DequeueGameRecvObject()
        {
            // Try to dequeue one, returning the result if successful or null if failed.
            return GameRecvQueue.TryDequeue(out GameRecvObject? result) ? result : null;
        }

        /// <summary>
        /// Dequeues all GameRecvObjects in the queue at the instant this is called and returns
        ///  as an array. Any objects enqueued during this method's execution will not be dequeued.
        /// </summary>
        /// <returns> The array of GameRecvObjects pulled from the queue. </returns>
        public GameRecvObject?[] DequeueAllGameRecvObjects()
        {
            // Store current number of items in the queue, then create an array of exactly this size.
            int count = GameRecvQueue.Count;
            GameRecvObject?[] tempArray = new GameRecvObject[count];

            // Dequeue and add to array the stored number of queued items. Will ignore any items
            //  added to the queue while this method is executing (prevents potential infinite block).
            for (int i = 0; i < count; i++)
            {
                GameRecvQueue.TryDequeue(out GameRecvObject? gameRecvObject);
                tempArray[i] = gameRecvObject;
            }

            return tempArray;
        }





        #region Worker Classes

        /// <summary>
        /// This class is responsible for managing the Serialization/Deserialization thread.
        /// </summary>
        private class SerializeWorker
        {
            private readonly Thread thread;
            private readonly Serializer serializer;
            private volatile bool shouldExit = false;

            internal SerializeWorker()
            {
                // Thread will call the Run() method.
                thread = new(Run);

                // Startup serializer (but do not begin running).
                serializer = new Serializer(Instance.GameSendQueue, Instance.NetSendQueue,
                    Instance.GameRecvQueue, Instance.NetRecvQueue);
            }



            /// <summary>
            /// Starts the worker thread, beginning serialization/deserialization operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                thread.Start();
                Console.WriteLine("[STARTUP] Starting serialization thread.");
            }

            /// <summary>
            /// Stops the worker thread, waiting for any remaining work to finish before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Sets shouldExit to true, which will gracefully exit the threaded loop.
                shouldExit = true;

                // Wait for the Server.Run() function to return, then join the thread (BLOCKS).
                Console.WriteLine("[EXIT] Waiting for serialization thread to stop...");
                thread.Join();
                Console.WriteLine("[EXIT] Serialization thread stopped successfully.");
            }

            /// <summary>
            /// Performs serialization/deserialization operations continuously until 'shouldExit' is true.
            /// </summary>
            private void Run()
            {
                while (!shouldExit)
                {
                    // Reads from game out queue, serializes, adds to network send queue.
                    serializer.DoGameToNetTasks();

                    // Reads from network receive queue, deserializes, adds to game in queue.
                    serializer.DoNetToGameTasks();
                }
            }
        }

        /// <summary>
        /// This class is responsible for managing the Network/ENet thread.
        /// </summary>
        private class ServerWorker
        {
            private readonly Thread thread;
            private readonly Server server;
            private volatile bool shouldExit = false;

            internal ServerWorker(ushort port)
            {
                // Thread will call the Run() method.
                thread = new(Run);

                // INITIALIZE ENET SERVER, BUT DO NOT CREATE SERVER YET.
                server = new Server(Instance.NetSendQueue, Instance.NetRecvQueue);
                server.SetHostParameters(port: port);   // SET HOST PARAMETERS HERE (HAVE DEFAULT VALUES)
            }



            /// <summary>
            /// Starts the worker thread, beginning server operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                thread.Start();
                Console.WriteLine("[STARTUP] Server host started listening on {0}:{1}",
                    server.GetAddress().GetIP(), server.GetAddress().Port);
            }

            /// <summary>
            /// Stops the worker thread, waiting for server to shut down before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Sets shouldExit to true, which will gracefully exit the threaded loop.
                shouldExit = true;

                // Wait for the Run() function to return, then join the thread (BLOCKS).
                Console.WriteLine("[EXIT] Waiting for server to shut down...");
                thread.Join();
                Console.WriteLine("[EXIT] Server shut down successfully.");
            }

            /// <summary>
            /// Creates server, then performs ENet networking tasks until 'shouldExit' is true, then stops server.
            /// </summary>
            private void Run()
            {
                // Start server, opening the host up for incoming connections and messages.
                server.Start();

                while (!shouldExit)
                {
                    // Reads from network send queue, then queues ENet operations.
                    server.DoNetSendTasks();

                    // Handles ENet events, runs host service, and adds to network receive queue.
                    server.DoNetReceiveTasks();
                }

                // Stop server, waiting at least 3 seconds for all clients to be disconnected properly.
                server.Stop();
            }
        }

        /// <summary>
        /// This class is responsible for managing the Network/ENet thread.
        /// </summary>
        private class ClientWorker
        {
            private readonly Thread thread;
            private readonly Client client;
            private volatile bool shouldExit = false;

            internal ClientWorker(ushort port)
            {
                // Thread will call the Run() method.
                thread = new(Run);

                // INITIALIZE ENET CLIENT, BUT DO NOT CREATE HOST YET.
                client = new Client(Instance.NetSendQueue, Instance.NetRecvQueue);
                client.SetHostParameters(port: port);
            }



            /// <summary>
            /// Starts the worker thread, beginning server operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                thread.Start();
                Console.WriteLine("[STARTUP] Client host started listening on {0}:{1}",
                    client.GetAddress().GetIP(), client.GetAddress().Port);
            }

            /// <summary>
            /// Stops the worker thread, waiting for server to shut down before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Sets shouldExit to true, which will gracefully exit the threaded loop.
                shouldExit = true;

                // Wait for the Run() function to return, then join the thread (BLOCKS).
                Console.WriteLine("[EXIT] Waiting for client to shut down...");
                thread.Join();
                Console.WriteLine("[EXIT] Server shut down successfully.");
            }

            /// <summary>
            /// Creates server, then performs ENet networking tasks until 'shouldExit' is true, then stops server.
            /// </summary>
            private void Run()
            {
                // Start client, attempting to connect to the host at the configured remote Address.
                client.Start();

                while (!shouldExit)
                {
                    // Reads from network send queue, then queues ENet operations.
                    client.DoNetSendTasks();

                    // Handles ENet events, runs host service, and adds to network receive queue.
                    client.DoNetReceiveTasks();
                }

                // Stop client, waiting at least 3 seconds to successfully disconnect from remote host.
                client.Stop();
            }
        }

        #endregion
    }
}
