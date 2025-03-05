using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetObjects.DataObjects.TransformDataObject;

namespace ENetServer.NetObjects.DataObjects
{
    public class TextDataObject : GameDataObject
    {
        public string String { get; }

        private TextDataObject(Builder builder) : base(DataType.Text)
        {
            String = builder.String;
        }



        public override byte[] Serialize()
        {
            // Convert all data into raw byte arrays to be concatenated below.
            byte[] headerBytes = [(byte)DataType];
            byte[] stringBytes = NetHelpers.GetBytes(String);

            // Concat all arrays together in this specific order, then return.
            byte[] bytes = NetHelpers.ConcatByteArrays(headerBytes, stringBytes);
            return bytes;
        }

        public override string GetDescription()
        {
            return "TextDataObject contents: " + String;
        }



        /// <summary>
        /// This Builder is required to create objects of this class. Allows building from raw data or
        ///  deserializing from a byte array.
        /// </summary>
        public class Builder
        {
            public string String { get; private set; } = string.Empty;

            public Builder()
            {
                // Default constructor
            }



            public Builder FromString(string str)
            {
                String = NetHelpers.FormatStringForSend(str);
                return this;
            }

            public Builder FromByteArray(byte[] bytes)
            {
                string tempString = NetHelpers.GetString(bytes, 0, bytes.Length);
                String = NetHelpers.FormatStringFromReceive(tempString);

                return this;
            }

            public TextDataObject Build()
            {
                return new TextDataObject(this);
            }
        }
    }
}
