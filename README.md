# Development Server

Exploratory multithreaded C# networking solution built around the ENet networking library.

---

## Thread Structure
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

---

## Performance Documentation

The below section contains performance documentation from tests during development. This is measured in round-trip test send operations, in which new GameDataObjects are passed along from main/game->serialize->net, then back from net->serialize->main/game (without actually sending). This tests the inter-system performance. All tests below were run with 10,000,000 Text GameDataObjects created and enqueued, then waited for all to return through the system.

**Fully parallel operation (one enqueue and one dequeue attempt per while loop iteration):** 
* Send operations completed after ~4500-5000ms
* Receive operations completed after ~7500-8000ms
* Average round-trip operations per second: ~1.25-1.3 million

**Parallel operation with explicit wait to dequeue (enqueue one then block until that item is dequeued, each iteration):** 
* Send and receive operations (both enqueue and dequeue happening in same while loop iteration) completed after ~14000ms
* Average round-trip operations per second: ~700,000

**Linear operation (enqueue all objects first, then dequeue all objects after enqueue is fully complete):** 
* Send operations completed after ~4500ms
* Receive operations completed after ~7500-8000ms
* Average round-trip operations per second: ~1.25-1.3 million

---

## Development Notes (Performance Branches)

This project contains a few additional branches which implement alternative systems:
1. Implementing static object pooling within NetObjects to reduce the number of objects instantiated and garbage collected.
2. Implementing an additional set of queues which communicate directly between the game and network threads, used for non-serializable data (ex. Connect, Disconnect).
3. Consolidating different NetObjects into one for send and one for receive to reduce the number of objects instantiated and garbage collected.

**Static pooling**

Each NetObject's static Factory implemented its own object pool using a ConcurrentQueue to get and return objects to the pool. The Factory implemented methods to return an object to the pool and to set the capacity of the pool. This pooling *decreased* performance by ~10% in tests, which in hindsight is likely because of the increased number of method calls and individual statements being executed in order to get and return pooled objects. As the NetObjects are all quite simple (each at the time including only a single uint and two reference types), object pooling was actually not necessary. If more complex objects are frequently created and destroyed in the future, object pooling should be revisited.

**Additional direct queues**

The idea behind direct queues for game->net and net->game was to prevent the serializer from ever having to interact with non-serializable (non-message) objects, like Connect or Disconnect actions. Implementing this included changing the existing four queues (game->serialize, serialize->net, net->serialize, serialize->game) to contain *only* serializable message objects, while the two new queues contain all non-serializable action objects. The network and game threads had to read from two separate queues each iteration rather than one, but the serializer did not have to perform any SendType/RecvType checks to determine whether to try to serialize/deserialize the object. Implementing this resulted in a ~30% *decrease* in performance, which is likely because of doubling the number of interactions with ConcurrentQueues within the game and network threads. Checking Count or IsEmpty each iteration is likely an expensive process, so doubling the number of these checks probably contributed primarily to the dramatic decrease in performance. I cannot envision a situation where this direct queue change will be revisited in the future.

**Consolidated NetObjects**

Consolidating NetObjects intended to reduce the number of objects being instantiated and garbage collected each iteration by the serialization thread. As it stands, the serialization thread reads GameSendObjects and NetRecvObjects, then creates new NetSendObjects and GameRecvObjects after handling the data (may or may not serialize/deserialize). Logically, this is wasteful and it makes much more sense to use the same object to contain both serialized and deserialized data (byte[] and GameDataObject), simply setting and nullifying the data as needed. The system was modified to do exactly this, but *surprisingly reduced* performance by ~30%. While reduced performance with the previous two performance-intended modifications can be explained in a few different ways, that does not seem to be the case here. I do not know why consolidating NetObjects to reduce the number of objects created/destroyed would reduce performance like this.
