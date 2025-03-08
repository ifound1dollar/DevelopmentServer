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
    //  2. Below within Deserialize(), building a GameDataObject of the proper class via switch on
    //      DataType.
    //  3. Create a new subclass of GameDataObject which corresponds to the new DataType. This must
    //      implement abstract methods and a Builder to instantiate, serialize, and deserialize
    //      objects of this new corresponding DataType.



    public abstract class GameDataObject(DataType dataType)
    {
        // This readonly Property uses primary constructor - subclasses call base(DataType.whatever) to set this on construction.
        public DataType DataType { get; } = dataType;

        // This method will be overridden by each subclass to implement proper serialization.
        public abstract byte[] Serialize();
        public abstract string GetDescription();



        /// <summary>
        /// Attemps to deserializes a byte[] into a valid GameDataObject instance.
        /// </summary>
        /// <param name="bytes"> The byte[] to try to deserialize. </param>
        /// <returns> The deserialized GameDataObject, or null if unsuccessful. </returns>
        public static GameDataObject? Deserialize(byte[] bytes)
        {
            // Directly cast first byte in byte[] to DataType (if data is correctly formatted, this is fine).
            DataType dataType = (DataType)bytes[0];

            switch (dataType)
            {
                case DataType.Text:
                    {
                        return new TextDataObject.Builder()
                            .FromByteArray(bytes)
                            .Build();
                    }
                case DataType.Transform:
                    {
                        return new TransformDataObject(bytes);
                    }
            }

            // In the case of DataType.None or default (IMPLICITLY REACHES HERE), return null.
            //Console.WriteLine("[ERROR] Received byte[] contains malformed DataType byte.");
            return null;
        }
    }
}
