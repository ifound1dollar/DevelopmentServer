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
    // NOTE: Adding a new DataType requires modifications in:
    //  1. Here! Make a new DataType.
    //  2. Within GameOutDataObject in TWO places: First within CheckIsValid() to verify data is not
    //      malformed, second by creating a new static template method (i.e. MakeDataTypeMethodName()).
    //  3. Within Serializer in TWO places: First within SerializeGameOutObject() to properly handle
    //      the type of data being serialized, and second within DeserializeGameInObject() to do the
    //      same but in reverse.



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
                        return null;    // TODO: IMPLEMENT TEXT OBJECT
                    }
                case DataType.Transform:
                    {
                        return new TransformDataObject.Builder()
                            .FromByteArray(bytes)
                            .Build();   //TODO: ANALYZE EFFICIENCY WITH USING A BUILDER (two objects created here)
                    }
            }

            // In the case of DataType.None or default (IMPLICITLY REACHES HERE), return null.
            //Console.WriteLine("[ERROR] Received byte[] contains malformed DataType byte.");
            return null;
        }
    }
}
