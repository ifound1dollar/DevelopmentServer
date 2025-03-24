# Development Server

Exploratory multithreaded C# networking solution built around the ENet networking library.

---

**The main project in this solution is ENetServer, a threaded networking system (WIP) using ENet designed for use in multiplayer video games. It runs three separate threads:**
1. Main thread (or the game thread in game engines like Unity and Unreal Engine)
2. Serialization/deserialization thread
3. Network thread

The main/game thread handles user operations in the test application, or main-thread Engine operations (rendering, user input, game logic, etc.) if used with a game engine. The main thread can safely access the NetworkManager singleton and its public methods, allowing it to enqueue and dequeue NetObjects from the main-thread-accessible ConcurrentQueues. The main/game thread only needs to create GameSendObjects using its static Factory and enqueue them, the NetworkManager handles the rest. Additionally, the ENetServer project defines a class that can be used to simulate Tick operation in a game engine (GameSimulator.cs).

The serialization/deserialization thread exists exclusively to communicate data between the main/game thread and the network thread, serializing and deserializing data accordingly. For outgoing data, non-serialized game data is passed from the main/game thread to this thread and then is serialized and passed from this thread to the network thread. The reverse is done when passing data from the network thread to the main/game thread. The Serializer works in isolation primarily as a communicator between the other threads. If necessary, the system can be augmented to support multiple serialization threads if necessary for performance reasons.

The network thread handles all ENet operations and is responsible for reading from and adding to the network send/receive ConcurrentQueues. This thread starts, stops, and runs the ENet Host, which enqueues and handles incoming and outgoing network events. All low-level networking tasks are performed within this thread using the ENet networking library. This thread is strictly responsible for networking tasks and includes only the data necessary for it to function properly (notably, a map of Peers and Connection objects).

**Additionally, two example classes exist to test the ENet server: ExampleServer and ExampleClient:**

As the names suggest, each project implements a fully-functional Server or Client NetworkManager instance. The server runs on port 7777 by default, and the client runs on port 8888 by default. The port can be specified using the command line when the program is run, and the default values can be modified within the NetworkManager class at the ServerPortMin/ClientPortMin properties and at the Setup() methods' default parameters.

Each example class starts the NetworkManager as a client or server, starts the GameSimulator thread, then executes a continuous while loop to get user input to run commands. The GameSimulator exists only to dequeue GameRecvObjects as a game engine would on Tick; input actions must be handled in real-time using the application's main thread while loop.


## Performance documentation:

The below section contains various performance documentation from tests during development. This is primarily to show the networking solution's evolution over time and the benefit of extra performance considerations. Measured primarily in round-trip test send operations, in which new GameDataObjects are passed along from main/game->serialize->net, then back from net->serialize->main/game.

**Multi-threaded implementation *without* NetObject static object pools, 10,000,000 Text GameDataObjects:**

Fully parallel operation (one enqueue and one dequeue attempt per while loop iteration):
* Send operations completed after ~4500-5000ms
* Receive operations completed after ~7500-8000ms
* Average round-trip operations per second: ~1.25-1.3 million

Parallel operation with explicit wait to dequeue (enqueue one then block until that item is dequeued, each iteration):
* Send and receive operations (both enqueue and dequeue happening in same while loop iteration) completed after ~14000ms
* Average round-trip operations per second: ~700,000

Linear operation (enqueue all objects, then dequeue all objects individually):
* Send operations completed after ~4500ms
* Receive operations completed after ~7500-8000ms
* Average round-trip operations per second: ~1.25-1.3 million

**Multi-threaded implementation *with* NetObject static object pools, 10,000,000 Text GameDataObjects: (OLD BEFORE FULL SYSTEM FUNCTIONALITY)**

Fully parallel operation:
* Send operations completed after ~5500ms
* Receive operations completed after ~8500ms
* Average round-trip operations per second: ~1.2 million