using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetHelpers;

namespace ENetServer.Deprecated
{
    /// <summary>
    /// Data object containing NON-SERIALIZED game data TO BE SENT over the network. Must use Builder to create objects.
    /// </summary>
    [Obsolete("Deprecated, use GameSendObject instead.", true)]
    public class GameOutDataObject
    {
        public SendType SendType { get; }
        public uint PeerID { get; }
        public DataType DataType { get; }

        public bool[]? Bools { get; }
        public short[]? Shorts { get; }
        public int[]? Ints { get; }
        public uint[]? UInts { get; }
        public long[]? Longs { get; }
        public float[]? Floats { get; }
        public double[]? Doubles { get; }
        public string? String { get; }

        private GameOutDataObject(Builder builder)
        {
            SendType = builder.SendType;
            PeerID = builder.PeerID;
            DataType = builder.DataType;

            // Initialize and set data arrays from Builder data, checking if Lists are null and converting to Array.
            Bools = builder.Bools?.ToArray();
            Shorts = builder.Shorts?.ToArray();
            Ints = builder.Ints?.ToArray();
            UInts = builder.UInts?.ToArray();
            Longs = builder.Longs?.ToArray();
            Floats = builder.Floats?.ToArray();
            Doubles = builder.Doubles?.ToArray();
            String = builder.String?.ToString();    // May just be setting to null again.
        }



        /// <summary>
        /// Builder used to create new GameOutDataObject instances.
        /// </summary>
        public class Builder
        {
            private bool sendTypeSet;

            public SendType SendType { get; private set; }
            public uint PeerID { get; private set; }
            public DataType DataType { get; private set; }

            public List<bool>? Bools { get; private set; }
            public List<short>? Shorts { get; private set; }
            public List<int>? Ints { get; private set; }
            public List<uint>? UInts { get; private set; }
            public List<long>? Longs { get; private set; }
            public List<float>? Floats { get; private set; }
            public List<double>? Doubles { get; private set; }
            public StringBuilder? String { get; private set; }

            /// <summary>
            /// Builder requires SendType to be defined.
            /// </summary>
            public Builder()
            {
                // Default constructor
            }



            public Builder AddSendType(SendType sendType)
            {
                // Validate builder once SendType is set via this method at least once.
                sendTypeSet = true;

                SendType = sendType;
                return this;
            }

            public Builder AddPeerID(uint peerId)
            {
                PeerID = peerId;
                return this;
            }

            public Builder AddDataType(DataType dataType)
            {
                DataType = dataType;
                return this;
            }

            #region Bool
            public Builder AddBool(bool inBool)
            {
                Bools ??= new List<bool>();     // Can give this default capacity for efficiency

                Bools.Add(inBool);
                return this;
            }
            public Builder AddBools(bool[] inBools)
            {
                Bools ??= new List<bool>();

                Bools.AddRange(inBools);
                return this;
            }
            #endregion

            #region Short
            public Builder AddShort(short inShort)
            {
                Shorts ??= new List<short>();

                Shorts.Add(inShort);
                return this;
            }
            public Builder AddShorts(short[] inShorts)
            {
                Shorts ??= new List<short>();

                Shorts.AddRange(inShorts);
                return this;
            }
            #endregion

            #region Int
            public Builder AddInt(int inInt)
            {
                Ints ??= new List<int>();

                Ints.Add(inInt);
                return this;
            }
            public Builder AddInts(int[] inInts)
            {
                Ints ??= new List<int>();

                Ints.AddRange(inInts);
                return this;
            }
            #endregion

            #region UInt
            public Builder AddUInt(uint inUInt)
            {
                UInts ??= new List<uint>();

                UInts.Add(inUInt);
                return this;
            }
            public Builder AddUInts(uint[] inUInts)
            {
                UInts ??= new List<uint>();

                UInts.AddRange(inUInts);
                return this;
            }
            #endregion

            #region Long
            public Builder AddLong(long inLong)
            {
                Longs ??= new List<long>();

                Longs.Add(inLong);
                return this;
            }
            public Builder AddLongs(long[] inLongs)
            {
                Longs ??= new List<long>();

                Longs.AddRange(inLongs);
                return this;
            }
            #endregion

            #region Float
            public Builder AddFloat(float inFloat)
            {
                // Instantiate new list if null
                Floats ??= new List<float>();   // Can give this default size for efficiency

                Floats.Add(inFloat);
                return this;
            }
            public Builder AddFloats(float[] inFloats)
            {
                // Instantiate new list if null
                Floats ??= new List<float>();

                Floats.AddRange(inFloats);
                return this;
            }
            #endregion

            #region Double
            public Builder AddDouble(double inDouble)
            {
                // Instantiate new list if null
                Doubles ??= new List<double>();

                Doubles.Add(inDouble);
                return this;
            }
            public Builder AddDoubles(double[] inDoubles)
            {
                // Instantiate new list if null
                Doubles ??= new List<double>();

                Doubles.AddRange(inDoubles);
                return this;
            }
            #endregion

            #region String
            public Builder AddString(string inString)
            {
                // Create new StringBuilder if null.
                String ??= new StringBuilder();

                String.Append(inString);
                return this;
            }
            public Builder AddStrings(string[] inStrings)
            {
                // Create new StringBuilder if null, then add each in string to it.
                String ??= new StringBuilder();
                foreach (string str in inStrings)
                {
                    String.Append(str);
                }

                return this;
            }
            #endregion

            /// <summary>
            /// Constructs and returns a new GameOutDataObject with this Builder's data.
            /// </summary>
            /// <returns> The newly constructed GameOutDataObject. </returns>
            public GameOutDataObject Build()
            {
                if (CheckIsValid(out string errorMessage))
                {
                    return new GameOutDataObject(this);
                }
                else
                {
                    throw new InvalidOperationException(errorMessage);
                }
            }

            /// <summary>
            /// Checks whether this Builder instance is valid according to its DataType.
            /// </summary>
            /// <param name="errorMessage"> An error message string to be used if this Builder is malformed. </param>
            /// <returns> Whether this Builder is valid and not malformed. </returns>
            private bool CheckIsValid(out string errorMessage)
            {
                errorMessage = string.Empty;

                // SendType must be manually set, else object is likely not initialized correctly.
                if (!sendTypeSet)
                {
                    errorMessage = "SendType must be explicitly set within Builder via AddSendType() method.";
                    return false;
                }

                // Check whether proper data values are set here.
                switch (DataType)
                {
                    case DataType.Text:
                        {
                            // String must be set.
                            if (String != null)
                            {
                                return true;
                            }

                            errorMessage = "TEXT DataType requires String value to be set.";
                            return false;
                        }
                    case DataType.Transform:
                        {
                            // UInts must have 1 value (Actor ID) and Doubles must have 9 values (Location, Rotation, Scale).
                            if (UInts != null && UInts.Count == 1
                                && Doubles != null && Doubles.Count == 9)
                            {
                                return true;
                            }

                            errorMessage = "TRANSFORM DataType requires 1 UInts value and 9 Doubles values.";
                            return false;
                        }

                }

                // Will only reach here if DataType is NONE or a case is not caught, so return sendTypeSet.
                return sendTypeSet;
            }
        }



        #region Static Template Methods

        /// <summary>
        /// Creates a generic 'disconnect all' GameOutDataObject.
        /// </summary>
        /// <returns> The created GameOutDataObject. </returns>
        public static GameOutDataObject MakeGenericDisconnectAll()
        {
            GameOutDataObject dataObject = new Builder()
                .AddSendType(SendType.Disconnect_All)
                .Build();
            return dataObject;
        }

        /// <summary>
        /// Creates a generic 'message all' GameOutDataObject with the passed-in message string.
        /// </summary>
        /// <param name="message"> Message to send to all connected clients. </param>
        /// <returns> The created GameOutDataObject. </returns>
        public static GameOutDataObject MakeGenericMessageAll(string message)
        {
            GameOutDataObject dataObject = new Builder()
                .AddSendType(SendType.Message_All)
                .AddDataType(DataType.Text)
                .AddString(message)
                .Build();
            return dataObject;
        }

        /// <summary>
        /// Creates a transform GameOutDataObject corresponding to a single actor that is sent to all clients.
        /// </summary>
        /// <param name="actorId"> ID of Actor corresponding to the transform. </param>
        /// <param name="location"> Location component of the transform. </param>
        /// <param name="rotation"> Rotation component of the transform. </param>
        /// <param name="scale"> Scale component of the transform. </param>
        /// <returns> The created GameOutDataObject. </returns>
        public static GameOutDataObject MakeActorTransformAll(uint actorId, double[] location, double[] rotation, double[] scale)
        {
            GameOutDataObject dataObject = new Builder()
                .AddSendType(SendType.Message_All)
                .AddDataType(DataType.Transform)
                .AddUInt(actorId)
                .AddDoubles(location)
                .AddDoubles(rotation)
                .AddDoubles(scale)
                .Build();
            return dataObject;
        }

        #endregion
    }
}
