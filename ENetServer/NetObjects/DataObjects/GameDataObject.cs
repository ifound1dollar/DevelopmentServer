using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    public enum DataType : byte
    {
        None = 0,
        Text = 1,
        Transform = 2,
    }
    // NOTE: Adding a new DataType requires a few operations:
    //  1. Here! Make a new DataType.
    //  2. Below within DeserializeGameDataObject(), creating a new GameDataObject of the proper class
    //      via switch on DataType enum.
    //  3. Create a new subclass of GameDataObject which corresponds to the new DataType. This must
    //      implement abstract methods and a static Factory with methods to create instances via
    //      default data and deserialization.



    public abstract class GameDataObject(DataType dataType)
    {
        // This readonly Property uses primary constructor - subclasses call base(DataType.whatever)
        //  to set this on construction.
        public DataType DataType { get; } = dataType;



        public abstract byte[] Serialize();
        public abstract string GetDescription();



        /// <summary>
        /// Attempts to serialize the passed-in GameDataObject into a byte[]. User should
        ///  verify success immediately after calling this method.
        /// </summary>
        /// <param name="dataObject"> The GameDataObject attempting to be serialized. May be null. </param>
        /// <returns> The serialized GameDataObject as a byte[], or null if unsuccessful (null argument). </returns>
        public static byte[]? SerializeGameDataObject(GameDataObject? dataObject)
        {
            // Return null if argument GameDataObject is null.
            if (dataObject == null) return null;

            // If argument GameDataObject is not null, serialize into byte[].
            byte[] bytes = dataObject.Serialize();  // Will prepend DataType value byte within this method.

            //foreach (byte b in bytes)
            //{
            //    Console.Write(b + " "); // TODO: REMOVE THIS TEMP TEST PRINT
            //}
            //Console.WriteLine();

            // Return the GameDataObject serialized into a raw byte[].
            return bytes;
        }

        /// <summary>
        /// Attemps to deserialize a byte[] into a valid GameDataObject instance. User should
        ///  verify success immediately after calling this method.
        /// </summary>
        /// <param name="bytes"> The byte[] attempting to be deserialized. </param>
        /// <returns> The deserialized GameDataObject, or null if unsuccessful (malformed or null byte[]). </returns>
        public static GameDataObject? DeserializeGameDataObject(byte[]? bytes)
        {
            // Ensure argument byte[] is non-null, then directly cast first byte in byte[] to DataType.
            if (bytes == null) return null;
            DataType dataType = (DataType)bytes[0];

            // Switch on the DataType byte to determine which class' deserialization method to call.
            GameDataObject? dataObject = null;
            switch (dataType)
            {
                case DataType.Text:
                    {
                        dataObject = TextDataObject.Factory.CreateFromDeserialize(bytes);
                        break;
                    }
                case DataType.Transform:
                    {
                        dataObject = TransformDataObject.Factory.CreateFromDeserialize(bytes);
                        break;
                    }
            }

            // DataType.None or default cases are implicitly caught here - GameDataObject remains null.
            return dataObject;
        }
    }
}
