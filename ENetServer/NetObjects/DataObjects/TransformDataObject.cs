using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    public class TransformDataObject : GameDataObject
    {
        public enum TransformType : byte { None, Location, Rotation, Scale,
            LocationRotation, LocationScale, RotationScale, FullTransform }



        public TransformType TransformLevel { get; }
        public uint ActorID { get; }
        public double[] Doubles { get; }

        private TransformDataObject(Builder builder) : base(DataType.Transform)
        {
            TransformLevel = builder.TransformType;
            ActorID = builder.ActorID;
            Doubles = builder.Doubles;
        }



        public override byte[] Serialize()
        {
            // Convert all data into raw byte arrays to be concatenated below.
            byte[] headerBytes = [(byte)DataType, (byte)TransformLevel];
            byte[] actorIdBytes = NetHelpers.GetBytes(ActorID);
            byte[] doubleBytes = NetHelpers.GetBytes(Doubles);

            // Concat all arrays together in this specific order, then return.
            byte[] bytes = NetHelpers.ConcatByteArrays(headerBytes, actorIdBytes, doubleBytes);
            return bytes;
        }

        public override string GetDescription()
        {
            return "TransformDataObject TEMP description.";
        }



        /// <summary>
        /// This Builder is required to create objects of this class. Allows building from raw data or
        ///  deserializing from a byte array.
        /// </summary>
        public class Builder
        {
            public TransformType TransformType { get; private set; }
            public uint ActorID { get; private set; }
            public double[] Doubles { get; private set; } = [];

            public Builder()
            {
                // Default constructor
            }



            public Builder FromLocation(uint actorId, double locX, double locY, double locZ)
            {
                TransformType = TransformType.Location;
                ActorID = actorId;
                Doubles = [locX, locY, locZ];

                return this;
            }

            //TODO: IMPLEMENT REMAINING TRANSFORM TYPE BUILDER METHODS

            public Builder FromByteArray(byte[] bytes)
            {
                // Cast byte at index 1 (after DataType byte which is already known) directly to TransformType.
                TransformType = (TransformType)bytes[1];

                // Start at index 2 (after DataType byte and TransformType byte), and implicit length 4.
                ActorID = NetHelpers.GetUInt(bytes, 2);

                // Start at index 6 (after DataType, TransformType, and actorId), with length of total array minus 6.
                Doubles = NetHelpers.GetDoubles(bytes, 6, bytes.Length - 6);

                return this;
            }

            public TransformDataObject Build()
            {
                return new TransformDataObject(this);
            }
        }
    }
}
