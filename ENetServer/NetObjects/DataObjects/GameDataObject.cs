using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// Attempts to serialize the passed-in GameDataObject into a byte[]. Returns whether successfully
        ///  serialized.
        /// </summary>
        /// <param name="gameDataObject"> The GameDataObject attempting to be serialized. May be null. </param>
        /// <param name="bytes"> The byte[] of deserialized data, or null if unsuccessful. </param>
        /// <returns> Whether the GameDataObject was successfully serialized. </returns>
        public static bool SerializeGameDataObject(GameDataObject? gameDataObject, [NotNullWhen(true)] out byte[]? bytes)
        {
            bytes = null;

            // If passed-in GameDataObject is null, return false with null byte[].
            if (gameDataObject == null) return false;

            // If argument GameDataObject is not null, serialize into byte[] and return.
            bytes = gameDataObject.Serialize();  // Will prepend DataType value byte within this method.

            //foreach (byte b in bytes)
            //{
            //    Console.Write(b + " "); // TODO: REMOVE THIS TEMP TEST PRINT
            //}
            //Console.WriteLine();

            return true;
        }

        /// <summary>
        /// Attempts to serialize the passed-in GameDataObject into a byte[]. Returns whether successfully
        ///  serialized.
        /// </summary>
        /// <param name="bytes"> The byte[] of serialized data attempting to be deserialized. </param>
        /// <param name="gameDataObject"> The GameDataObject deserialized from the byte[], or null if unsuccessful </param>
        /// <returns> Whether the byte[] was successfully deserialized. </returns>
        public static bool DeserializeGameDataObject(byte[]? bytes, [NotNullWhen(true)] out GameDataObject? gameDataObject)
        {
            gameDataObject = null;

            // If passed-in byte[] is null, return false with null GameDataObject.
            if (bytes == null) return false;

            // Get first byte of data as DataType enum.
            DataType dataType = (DataType)bytes[0];

            // Switch on the DataType byte to determine which class' deserialization method to call.
            switch (dataType)
            {
                case DataType.Text:
                    {
                        gameDataObject = TextDataObject.Factory.CreateFromDeserialize(bytes);
                        break;
                    }
                case DataType.Transform:
                    {
                        gameDataObject = TransformDataObject.Factory.CreateFromDeserialize(bytes);
                        break;
                    }
                // DataType.None or default cases are implicitly caught here, leaving GameDataObject null.
            }

            // Return whether GameDataObject is NOT null (not null meaning successful).
            return gameDataObject != null;
        }
    }
}
