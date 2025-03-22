# Development Server

Exploratory multithreaded C# networking solution built around the ENet networking library.

---

**The main project in this solution is ENetServer, a threaded networking prototype using ENet designed for use in the context of multiplayer video games. It utilizes three separate threads:**
1. Main thread (or the game thread in game engines like Unity and Unreal Engine)
2. Serialization/deserialization thread
3. Network thread

The main/game thread handles user operations in the test application, or main-thread Engine operations (rendering, user input, game logic, etc.) if used with a game engine. The main thread can safely access the NetworkManager singleton and its public methods, allowing it to enqueue and dequeue data objects from the main-thread-accessible NetworkManager queues. The main thread can never directly communicate with the network thread; the serialization/deserialization thread acts as the translator.

The serialization/deserialization thread exists exclusively to communicate data between the main/game thread and the network thread, serializing and deserializing data accordingly. For outgoing data, non-serialized game data is passed from the main/game thread to this thread and then is serialized and passed from this thread to the network thread. The opposite is done when passing data from the network thread to the main/game thread. This thread ensures that neither the main/game thread nor the network thread has to worry about properly formatting data for communication; this thread handles it completely.

The network thread handles all ENet operations and is responsible for reading from and adding to the network thread-safe (concurrent) queues. This thread starts, stops, and runs the ENet Host, which enqueues and handles incoming and outgoing network events. All low-level networking tasks are performed within this thread using the ENet networking library. This thread is strictly responsible for networking tasks, and will never communicate directly with the main/game thread.


## Performance documentation:

The below section contains various performance documentation from tests during development. This is primarily to show the networking solution's evolution over time and the benefit of extra performance considerations. Measured primarily in round-trip test send operations, in which new GameDataObjects are passed along from main/game->serialize->net, then back from net->serialize->main/game.

**Multi-threaded implementation *without* NetObject static object pools, 10,000,000 Text GameDataObjects:**

Fully parallel operation (one enqueue and one dequeue attempt per while loop iteration):
* Send operations completed after ~3000-4000ms
* Receive operations completed after ~6500-7000ms
* Average round-trip operations per second: ~1.45-1.5 million

Parallel operation with explicit wait to dequeue (enqueue one then block until that item is dequeued, each iteration):
* Send and receive operations (both enqueue and dequeue happening in same while loop iteration) completed after ~12500-13000ms
* Average round-trip operations per second: ~750,000-800,000

Linear operation (enqueue all objects, then dequeue all objects individually):
* Send operations completed after ~3400-3500ms
* Receive operations completed after ~7000ms
* Average round-trip operations per second: ~1.4 million

**Multi-threaded implementation *with* NetObject static object pools, 10,000,000 Text GameDataObjects:**

Fully parallel operation:
* Send operations completed after ~5000-5500ms
* Receive operations completed after ~8000-8500ms
* Average round-trip operations per second: ~1.2-1.25 million