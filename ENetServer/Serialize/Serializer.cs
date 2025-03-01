using ENet;
using ENetServer.DataObjects;
using ENetServer.Management;
using System;
using System.Collections.Concurrent;
using static ENetServer.NetHelpers;

namespace ENetServer.Serialize
{
    /// <summary>
    /// Encapsulates all serialization/deserialization operations within this class.
    /// </summary>
    internal class Serializer
    {
        // QUEUE REFERENCES
        private readonly ConcurrentQueue<GameOutDataObject> gameOutQueue;
        private readonly ConcurrentQueue<NetworkSendDataObject> netSendQueue;
        private readonly ConcurrentQueue<GameInDataObject> gameInQueue;
        private readonly ConcurrentQueue<NetworkRecvDataObject> netRecvQueue;
        private readonly ConcurrentDictionary<uint, Connection> connectionsDict;

        /// <summary>
        /// Constructs a Serializer object with references to game AND network concurrent queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send queue. </param>
        /// <param name="netRecvQueue"> Reference to network receive queue. </param>
        internal Serializer(ConcurrentQueue<GameOutDataObject> gameOutQueue,
            ConcurrentQueue<NetworkSendDataObject> netSendQueue,
            ConcurrentQueue<GameInDataObject> gameInQueue,
            ConcurrentQueue<NetworkRecvDataObject> netRecvQueue,
            ConcurrentDictionary<uint, Connection> connectionsDict)
        {
            this.gameOutQueue = gameOutQueue;
            this.netSendQueue = netSendQueue;
            this.gameInQueue = gameInQueue;
            this.netRecvQueue = netRecvQueue;
            this.connectionsDict = connectionsDict;
        }



        /// <summary>
        /// Handles game out tasks - read from game out queue, operate on data (serialize), add to network send queue.
        /// </summary>
        internal void DoGameToNetTasks()
        {
            // Loop until queue is empty.
            while (!gameOutQueue.IsEmpty)
            {
                // Try to dequeue item from serializeQueue, operating on the item if successful.
                if (!gameOutQueue.TryDequeue(out GameOutDataObject? gameOutObject)) break;

                // Operate based on send type.
                switch (gameOutObject.SendType)
                {
                    case SendType.DISCONNECT_ONE:
                        {
                            SendDisconnectOne(gameOutObject);
                            break;
                        }
                    case SendType.DISCONNECT_ALL:
                        {
                            SendDisconnectAll();
                            break;
                        }
                    case SendType.MESSAGE_ONE:
                        {
                            SendMessageOne(gameOutObject);
                            break;
                        }
                    case SendType.MESSAGE_ALL:
                        {
                            SendMessageAll(gameOutObject);
                            break;
                        }
                    case SendType.MESSAGE_ALLEXCEPT:
                        {
                            SendMessageAllExcept(gameOutObject);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Handles net receive tasks - read from network receive queue, operate on data (deserialize), add to game in queue.
        /// </summary>
        internal void DoNetToGameTasks()
        {
            // Loop until net receive queue is empty.
            while (!netRecvQueue.IsEmpty)
            {
                // Try to dequeue item from netRecvQueue, operating on the item if successful.
                if (!netRecvQueue.TryDequeue(out NetworkRecvDataObject? netReceiveData)) break;

                // Operate based on receive type.
                switch (netReceiveData.RecvType)
                {
                    case RecvType.CONNECT:
                        {
                            ReceiveConnect(netReceiveData);
                            break;
                        }
                    case RecvType.DISCONNECT:
                        {
                            ReceiveDisconnect(netReceiveData);
                            break;
                        }
                    case RecvType.TIMEOUT:
                        {
                            ReceiveTimeout(netReceiveData);
                            break;
                        }
                    case RecvType.MESSAGE:
                        {
                            ReceiveMessage(netReceiveData);
                            break;
                        }
                }
            }
        }



        #region Send Methods

        private void SendDisconnectOne(GameOutDataObject gameObject)
        {
            NetworkSendDataObject dataObject = new NetworkSendDataObject.Builder()
                .AddSendType(SendType.DISCONNECT_ONE)
                .AddPeerID(gameObject.PeerID)
                .Build();
            netSendQueue.Enqueue(dataObject);
        }

        private void SendDisconnectAll()
        {
            NetworkSendDataObject dataObject = new NetworkSendDataObject.Builder()
                .AddSendType(SendType.DISCONNECT_ALL)
                .Build();
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageOne(GameOutDataObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[] bytes = SerializeGameOutObject(gameObject);

            NetworkSendDataObject dataObject = new NetworkSendDataObject.Builder()
                .AddSendType(SendType.MESSAGE_ONE)
                .AddPeerID(gameObject.PeerID)
                .AddBytes(bytes)
                .Build();
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageAll(GameOutDataObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[] bytes = SerializeGameOutObject(gameObject);

            NetworkSendDataObject dataObject = new NetworkSendDataObject.Builder()
                .AddSendType(SendType.MESSAGE_ALL)
                .AddBytes(bytes)
                .Build();
            netSendQueue.Enqueue(dataObject);
        }

        private void SendMessageAllExcept(GameOutDataObject gameObject)
        {
            // Serialize GameOutObject data and return as byte[].
            byte[] bytes = SerializeGameOutObject(gameObject);

            NetworkSendDataObject dataObject = new NetworkSendDataObject.Builder()
                .AddSendType(SendType.MESSAGE_ALLEXCEPT)
                .AddPeerID(gameObject.PeerID)
                .AddBytes(bytes)
                .Build();
            netSendQueue.Enqueue(dataObject);
        }

        #endregion

        #region Receive Methods

        private void ReceiveConnect(NetworkRecvDataObject recvObject)
        {
            // Create new Connection and try to add to Connections dict. If failed to add, disconnect and log failure.
            Connection connection = new(recvObject.PeerID, recvObject.PeerIP, recvObject.PeerPort);
            if (!connectionsDict.TryAdd(connection.ID, connection))
            {
                // Create and enqueue disconnect request.
                NetworkSendDataObject dataObject = new NetworkSendDataObject.Builder()
                    .AddPeerID(connection.ID)
                    .AddBytes([])                           // Not necessary here
                    .AddSendType(SendType.DISCONNECT_ONE)
                    .Build();
                netSendQueue.Enqueue(dataObject);

                // Log connection failure.
                Console.WriteLine("[ERROR] New connection from " + connection.IP + ":" + connection.Port
                    + " could not be added to Connections list. Immediately disconnecting.");
                return;
            }
            
            // Else connection was successful.
            Console.WriteLine("[CONNECT] Client connected - ID: " + connection.ID +
                ", Address: " + connection.IP + ":" + connection.Port);
        }

        private void ReceiveDisconnect(NetworkRecvDataObject recvObject)
        {
            // Remove now-disconnected client from Connections dict and log disconnect.
            connectionsDict.Remove(recvObject.PeerID, out _);   // Discard out parameter

            Console.WriteLine("[DISCONNECT] Client disconnected - ID: " + recvObject.PeerID +
                ", Address: " + recvObject.PeerIP + ":" + recvObject.PeerPort);
        }

        private void ReceiveTimeout(NetworkRecvDataObject recvObject)
        {
            // Remove now-disconnected client from Connections dict and log disconnect.
            connectionsDict.Remove(recvObject.PeerID, out _);   // Discard out parameter

            Console.WriteLine("[TIMEOUT] Client timeout - ID: " + recvObject.PeerID +
                ", Address: " + recvObject.PeerIP + ":" + recvObject.PeerPort);

        }

        private void ReceiveMessage(NetworkRecvDataObject recvObject)
        {
            // Operate on and DESERIALIZE netReceiveData.
            string recvString = NetHelpers.FormatStringFromReceive(recvObject.Bytes);

            // Create GameDataObject after deserializing, then enqueue for game/main thread reading.
            GameInDataObject dataObject = new GameInDataObject.Builder()
                .AddPeerID(recvObject.PeerID)
                .AddTempDataString(recvString)
                .Build();
            gameInQueue.Enqueue(dataObject);



            /// ----- BELOW IS ENTIRELY TEMPORARY, SIMULATING GAME THREAD READING FROM GAME IN QUEUE -----
            if (!gameInQueue.TryDequeue(out GameInDataObject? tempGameObject)) return;

            // Output incoming data.
            Console.WriteLine("[MESSAGE] Packet received from - ID: " + tempGameObject.PeerID +
                                ", Message: " + tempGameObject.TempDataString);



        }

        #endregion



        #region Serialization Methods

        private static byte[] SerializeGameOutObject(GameOutDataObject dataObject)
        {
            byte[] bytes;

            // Switch on DataType, which strictly determines how data is formatted and how it should be interpreted.
            switch (dataObject.DataType)
            {
                case DataType.TEXT:
                    {
                        bytes = DoSerializeText(dataObject.String);
                        break;
                    }
                case DataType.TRANSFORM:
                    {
                        bytes = DoSerializeTransform(dataObject.UInts, dataObject.Doubles);
                        break;
                    }
                default:    // Also implicitly captures DataType.NONE
                    {
                        bytes = []; // Make empty array if no valid DataType.
                        break;
                    }
            }

            // Prepend DataType value as byte to the start of the byte[] so the receiver knows the data type.
            byte[] finalBytes = new byte[bytes.Length + 1];
            finalBytes[0] = (byte)dataObject.DataType;

            // Copy serialized data in 'bytes' to return array starting at index 1, leaving first element.
            bytes.CopyTo(finalBytes, 1);

            //foreach (byte b in finalBytes)
            //{
            //    Console.Write(b + " ");
            //}
            //Console.WriteLine();

            return finalBytes;
        }

        private static byte[] DoSerializeText(string inString)
        {
            // Format string into net-ready format and convert to raw byte array.
            string temp = NetHelpers.FormatStringForSend(inString);
            byte[] bytes = NetHelpers.CreateByteArrayFromUTF8String(temp);

            return bytes;
        }

        private static byte[] DoSerializeTransform(uint[] uints, double[] doubles)
        {
            // Convert both uints[] and doubles[] into raw byte arrays.
            byte[] uintArray = NetHelpers.GetBytes(uints);  // Should only be one uint in this array.
            byte[] doubleArray = NetHelpers.GetBytes(doubles);

            // Returned merged byte arrays.
            return NetHelpers.MergeByteArrays(uintArray, doubleArray);
        }

        #endregion
    }
}
