﻿using ENetServer.Serialize;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    // ----- NOTES ON ALWAYS USING AN ENTIRE TRANSFORM DATA STRUCTURE -----
    // Using the entire data structure (three full 3-vectors) uses 24 to 48 additional Bytes of
    //  data (one or two 3-vectors), which is not necessarily the most efficient in the optimal
    //  case. HOWEVER, it is the easiest to work with and objectively most efficient in the
    //  average case.
    // The most common transform data being sent will be Location and Rotation, no Scale. This
    //  will use on average 24 extra Bytes per packet being sent. This may seem inefficient, but
    //  a dynamic transform object (ex. can be any combination of the three Transform components)
    //  is much more complex to work with and maintain, and is more error-prone.
    // An alternative to dynamic transforms would be to send each individual component separately
    //  in their own packets. This is inefficient for multiple reasons:
    //      1. For the most common transform data (Location and Rotation), this would require
    //          passing 2x the data structures across the threads, which should be avoided.
    //      2. The combined header data used by UDP (8 Bytes), IP (unknown), and ENet (unknown)
    //          will almost certainly exceed the number of Bytes being saved by not sending
    //          the unnecessary Scale value (saves 24 Bytes, but header is probably larger).
    // Another alternative could be to have a separate DataType for each combination of transform
    //  components. This could be efficient network-wise, but would add a lot of ugly clutter
    //  and potentially difficult-to-maintain data structures. This would turn the single-
    //  transform structure (used currently) into a seven-structure amalgamation.
    // ALL IN ALL, the extra ~24 Bytes (on average) being sent over the network for each
    //  transform should not be a problem. Using a single-transform structure is much clearer
    //  and less error-prone than dynamic transforms, individual components, or a full array
    //  of different transform component combinations. For example, replicating 1000 transforms
    //  each second will only use 24 kilobytes of extra bandwidth. 1 million transforms (which
    //  is not realistic) would use 24 megabytes.
    // IF WE REACH A POINT WHERE BANDWIDTH BECOMES A CONCERN, WE CAN RE-EVALUATE THIS STRUCTURE.
    
    public class TransformDataObject : GameDataObject
    {
        public uint ActorID { get; }
        public double[] Doubles { get; }

        private TransformDataObject(uint actorId, double[] doubles) : base(DataType.Transform)
        {
            ActorID = actorId;
            Doubles = doubles;
        }



        public override int Serialize(out byte[] bytes)
        {
            // Create new ArrayBuffer and add data in specific order.
            ArrayBuffer arrayBuffer = new ArrayBuffer(77)
                .AddByte((byte)DataType)
                .AddUInt(ActorID)
                .AddDouble(Doubles[0])
                .AddDouble(Doubles[1])
                .AddDouble(Doubles[2])
                .AddDouble(Doubles[3])
                .AddDouble(Doubles[4])
                .AddDouble(Doubles[5])
                .AddDouble(Doubles[6])
                .AddDouble(Doubles[7])
                .AddDouble(Doubles[8]);
            
            bytes = arrayBuffer.Bytes;
            return arrayBuffer.Length;
        }

        public override string GetDescription()
        {
            StringBuilder sb = new();
            sb.Append("[TransformDataObject] ActorID: " + ActorID + " | Doubles: ");
            foreach (double d in Doubles)
            {
                sb.Append(d.ToString() + " ");
            }

            return sb.ToString();
        }



        /// <summary>
        /// Factory responsible for creating TransformDataObjects. Allows creating from default/raw
        ///  data or by deserializing from byte array.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Attemps to create and return a new TransformDataObject from default/raw data. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="actorId"> ID of Actor this transform corresponds to. </param>
            /// <param name="doubles"> Array of doubles containing location, rotation, and scale. Must have 9 elements. </param>
            /// <returns> The newly created TransformDataObject, or null if unsuccessful (invalid argument). </returns>
            public static TransformDataObject? CreateFromDefault(uint actorId, double[] doubles)
            {
                // Validate argument data. Double[] must be Length 9 (3 location, 3 rotation, 3 scale).
                if (doubles.Length != 9)
                {
                    return null;
                }

                // If data is valid, create a new TransformDataObject instance and return it.
                return new TransformDataObject(actorId, doubles);
            }

            /// <summary>
            /// Attempts to create and return a new TransformDataObject by deserializing from a byte[]. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="bytes"> Raw byte[] containing serialized TransformDataObject data. Must correctly formatted. </param>
            /// <param name="length"> Length of data in byte[] (allocated size with Array.Length is probably longer). </param>
            /// <returns> The newly created TransformDataObject, or null if unsuccessful (argument byte[] is malformed). </returns>
            public static TransformDataObject? CreateFromDeserialize(byte[] bytes, int length)
            {
                // Validate argument byte[]. 1 byte for DataType, 4 Bytes for ActorID (uint), 72 Bytes for 9-element double[].
                if (length != 77)
                {
                    return null;
                }

                // Create ArrayBuffer from incoming data, then read data in reverse order.
                ArrayBuffer arrayBuffer = new ArrayBuffer(bytes, length);

                double[] doubles = new double[9];
                doubles[8] = arrayBuffer.ReadDouble();
                doubles[7] = arrayBuffer.ReadDouble();
                doubles[6] = arrayBuffer.ReadDouble();
                doubles[5] = arrayBuffer.ReadDouble();
                doubles[4] = arrayBuffer.ReadDouble();
                doubles[3] = arrayBuffer.ReadDouble();
                doubles[2] = arrayBuffer.ReadDouble();
                doubles[1] = arrayBuffer.ReadDouble();
                doubles[0] = arrayBuffer.ReadDouble();

                uint actorId = arrayBuffer.ReadUInt();

                return new TransformDataObject(actorId, doubles);
            }
        }
    }
}
