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

## Connection Validation

The network thread implements robust connection validation to allow/reject/blacklist Peers. This is a multi-step process, automatically removing and blacklisting Peers which attempt to connect but fail validation. It is designed to only allow Peers which send a valid preliminary checksum and then immediately send a valid full login token. The server should maintain a connection to a Master Server, which will message the server before a Peer attempts to connect and pass it the Peer's temporary login token. Any Peer that attempts to connect must know its valid login token which has been previously communicated by the Master Server. This login token is used by the server to generate a ValidationData struct, which contains a non-cryptographically-secure 32-bit checksum of the full login token for initial connection validation (as well as the full login token and an expiration DateTime). Peers must calculate and send this checksum as raw data on connection attempt, then immediately send the full login token if this preliminary connection is successful. If validation fails for any reason (invalid checksum, invalid login token, or expired validation data), the Peer is immediately force disconnected by the server and blacklisted for an exponentially-increasing duration based on number of failed attemtps. A Peer is completely removed from the blacklist any time it successfully connects.

The connection process is as follows:

1. Peer (server or client) sends an initial connect request to a remote host, passing a 32-bit checksum generated from the full login token.
2. Remote host (server) receives this connection request and compares the received checksum against its map of current awaiting validation data (this server should have received a message previously from an existing trusted connection containing a login token for this expected new connection). If the checksum passes, the server adds the Peer to a map of Peers awaiting validation and waits for the Peer to send its full login token.
3. The Peer receives an automatic 'connection success' response from the initial non-validated connection, instructing it to immediately send its full login token as raw data to the server.
4. The server receives this login token and compares it against the same map of validation data. If the token is valid, the server adds the Peer to a final-stage pending Peers map and sends the Peer a 'validation success' ACK.
5. The Peer receives this validation ACK and knows that the connection has been successfully made. It adds the server to its list of valid connections, then sends the server a response ACK which acknowledges that it successfully received the validation ACK.
6. Finally, the server receives this final response ACK and can now consider the connection fully valid. It removes the Peer from the final-stage pending Peers map and adds it to the map of fully-connected Peers. The server waits for this final ACK to finalize the connection (instead of immediately after login token validation success) to ensure that the Peer is ready to accept messages before actually sending them. Otherwise, the server may send a regular message while the Peer is still in its 'pending' state, in which the Peer will reject the connection because it is expecting a specifically-formatted ACK message (not a regular message).



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
