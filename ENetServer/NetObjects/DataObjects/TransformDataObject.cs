using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    public class TransformDataObject : GameDataObject
    {
        public uint ActorID { get; }
        public double[] Doubles { get; }

        public TransformDataObject(uint actorId, double[] doubles) : base(DataType.Transform)
        {
            // TODO: ALTER THIS TO VERIFY DOUBLE[] LENGTH IN ARGUMENT - CANNOT CANCEL CONSTRUCTION, MAY
            //  HAVE TO USE AN ALTERNATIVE BUILDER/FACTORY (OR STATIC METHODS TO CREATE INSTANCES WHICH
            //  WILL VALIDATE AND POTENTIALLY RETURN NULL)
            // could just throw an exception, but strictly document what type of data is needed using ///
            ActorID = actorId;
            Doubles = doubles;
        }

        public TransformDataObject(byte[] bytes) : base(DataType.Transform)
        {
            // Start at index 1 (after DataType byte), and implicit length 4.
            ActorID = NetHelpers.GetUInt(bytes, 1);

            // Start at index 5 (after DataType and actorId), with length of total array minus 5.
            Doubles = NetHelpers.GetDoubles(bytes, 5, bytes.Length - 5);
        }



        public override byte[] Serialize()
        {
            // Convert all data into raw byte arrays to be concatenated below.
            byte[] headerBytes = [(byte)DataType];
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
    }
}
