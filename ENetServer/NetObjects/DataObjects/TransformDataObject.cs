using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    // ----- NOTES ON ALWAYS USING AN ENTIRE TRANSFORM DATA STRUCTURE -----
    // Using the entire data structure (three full 3-vectors) uses 24 to 48 additional bytes of
    //  data (one or two 3-vectors), which is not necessarily the most efficient in the optimal
    //  case. HOWEVER, it is the easiest to work with and objectively most efficient in the
    //  average case.
    // The most common transform data being sent will be Location and Rotation, no Scale. This
    //  will use on average 24 extra bytes per packet being sent. This may seem inefficient, but
    //  a dynamic transform object (ex. can be any combination of the three Transform components)
    //  is much more complex to work with and maintain, and is more error-prone.
    // An alternative to dynamic transforms would be to send each individual component separately
    //  in their own packets. This is inefficient for multiple reasons:
    //      1. For the most common transform data (Location and Rotation), this would require
    //          passing 2x the data structures across the threads, which should be avoided.
    //      2. The combined header data used by UDP (8 bytes), IP (unknown), and ENet (unknown)
    //          will almost certainly exceed the number of bytes being saved by not sending
    //          the unnecessary Scale value (saves 24 bytes, but header is probably larger).
    // Another alternative could be to have a separate DataType for each combination of transform
    //  components. This could be efficient network-wise, but would add a lot of ugly clutter
    //  and potentially difficult-to-maintain data structures. This would turn the single-
    //  transform structure (used currently) into a seven-structure amalgamation.
    // ALL IN ALL, the extra ~24 bytes (on average) being sent over the network for each
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



        public override byte[] Serialize()
        {
            // Convert all data into raw byte arrays to be concatenated below.
            byte[] headerBytes = [(byte)DataType];
            byte[] actorIdBytes = NetStatics.GetBytes(ActorID);
            byte[] doubleBytes = NetStatics.GetBytes(Doubles);

            // Concat all arrays together in this specific order, then return.
            byte[] bytes = NetStatics.ConcatByteArrays(headerBytes, actorIdBytes, doubleBytes);
            return bytes;
        }

        public override string GetDescription()
        {
            return "TransformDataObject TEMP description.";
        }



        /// <summary>
        /// Factory responsible for creating TransformDataObjects. Allows creating from default/raw
        ///  data or by deserializing from byte array.
        /// </summary>
        public static class Factory
        {
            //TODO: IMPLEMENT FACTORY HERE AND CHECK PERFORMANCE

            /// <summary>
            /// Attemps to create and return a new TransformDataObject from default/raw data. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="actorId"> ID of Actor this transform corresponds to. </param>
            /// <param name="doubles"> Array of doubles containing location, rotation, and scale. Must have 9 elements. </param>
            /// <returns> The newly created TransformDataObject, or null if unsuccessful (invalid argument). </returns>
            public static TransformDataObject? CreateFromDefault(uint actorId, double[] doubles)
            {
                // Validate argument data. Double[] must be length 9 (3 location, 3 rotation, 3 scale).
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
            /// <returns> The newly created TransformDataObject, or null if unsuccessful (argument byte[] is malformed). </returns>
            public static TransformDataObject? CreateFromDeserialize(byte[] bytes)
            {
                // Validate argument byte[]. 1 byte for DataType, 4 bytes for ActorID (uint), 72 bytes for 9-element double[].
                if (bytes.Length != 77)
                {
                    return null;
                }

                uint actorId = NetStatics.GetUInt(bytes, 1);
                double[] doubles = NetStatics.GetDoubles(bytes, 5, bytes.Length - 5);
                return new TransformDataObject(actorId, doubles);
            }
        }
    }
}
