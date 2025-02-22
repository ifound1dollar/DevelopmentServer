# Development Server

Exploratory C# networking solution, working with TCP/WebSocket as well as UDP via C# directly or using the ENet networking library.

---

**The main project in this solution is ENetServer, which implements a threaded networking prototype using ENet that is designed to be used in the context of multiplayer video games. It utilizes three separate threads:**
1. Main thread (or the game thread in game engines like Unity and Unreal Engine)
2. Serialization/deserialization thread
3. Network thread

The main/game thread handles user operations in the test application, or main-thread Engine operations (rendering, user input, game logic, etc.) if used with a game engine. The main thread can safely access the NetworkManager singleton and its public methods, allowing it to enqueue and dequeue data objects from the main-thread-accessible NetworkManager queues. The main thread can never directly communicate with the network thread; the serialization/deserialization thread acts as the translator.

The serialization/deserialization thread exists exclusively to communicate data between the main/game thread and the network thread, serializing and deserializing data accordingly. For outgoing data, non-serialized game data is passed from the main/game thread to this thread and then is serialized and passed from this thread to the network thread. The opposite is done when passing data from the network thread to the main/game thread. This thread ensures that neither the main/game thread nor the network thread has to worry about properly formatting data for communication; this thread handles it completely.

The network thread handles all ENet operations and is responsible for reading from and adding to the network thread-safe (concurrent) queues. This thread starts, stops, and runs the ENet Host, which enqueues and handles incoming and outgoing network events. All low-level networking tasks are performed within this thread using the ENet networking library. This thread is strictly responsible for networking tasks, and will never communicate directly with the main/game thread.
