using ENetServer.Serialize;
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
        ConnectData = 254,
        TEST = 255
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



        public abstract int Serialize(out byte[] bytes);
        public abstract string GetDescription();



        /// <summary>
        /// Serializes the passed-in non-null GameDataObject into a byte[]. Returns the length of
        ///  serialized data.
        /// </summary>
        /// <param name="gameDataObject"> The GameDataObject being serialized. Cannot be null. </param>
        /// <param name="bytes"> The byte[] of serialized data. </param>
        /// <returns> The length of the serialized data, as the byte[] allocated size may be larger. </returns>
        public static int SerializeGameDataObject(GameDataObject gameDataObject, out byte[] bytes)
        {
            // Serialize GameDataObject into byte[] and return its length.
            int length = gameDataObject.Serialize(out bytes);

            //foreach (byte b in Bytes)
            //{
            //    Console.Write(b + " "); // TODO: REMOVE THIS TEMP TEST PRINT
            //}
            //Console.WriteLine();

            return length;
        }

        /// <summary>
        /// Attempts to serialize the passed-in GameDataObject into a byte[]. Returns whether successfully
        ///  serialized.
        /// </summary>
        /// <param name="bytes"> The byte[] of serialized data attempting to be deserialized. </param>
        /// <param name="length"> The length of data contained in the byte[]. </param>
        /// <param name="gameDataObject"> The GameDataObject deserialized from the byte[], or null if unsuccessful </param>
        /// <returns> Whether the byte[] was successfully deserialized. </returns>
        public static bool DeserializeGameDataObject(byte[] bytes, int length,
            [NotNullWhen(true)] out GameDataObject? gameDataObject)
        {
            gameDataObject = null;

            // Get first byte of data as DataType enum.
            DataType dataType = (DataType)bytes[0];

            // Switch on the DataType byte to determine which class' deserialization method to call.
            switch (dataType)
            {
                case DataType.Text:
                    {
                        gameDataObject = TextDataObject.Factory.CreateFromDeserialize(bytes, length);
                        break;
                    }
                case DataType.Transform:
                    {
                        gameDataObject = TransformDataObject.Factory.CreateFromDeserialize(bytes, length);
                        break;
                    }
                case DataType.ConnectData:
                    {
                        // TODO: CREATE CONNECTDATAOBJECT
                        break;
                    }
                case DataType.TEST:
                    {
                        gameDataObject = TESTDataObject.Factory.CreateFromDeserialize(bytes, length);
                        break;
                    }
                // DataType.None or default cases are implicitly caught here, leaving GameDataObject null.
            }

            // Return whether GameDataObject is NOT null (not null meaning successful).
            return gameDataObject != null;
        }
    }
}
