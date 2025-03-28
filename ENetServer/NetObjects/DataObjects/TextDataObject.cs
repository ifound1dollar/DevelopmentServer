using ENetServer.Serialize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    public class TextDataObject : GameDataObject
    {
        public string String { get; }

        private TextDataObject(string str) : base(DataType.Text)
        {
            String = str;
        }



        public override int Serialize(out byte[] bytes)
        {
            // Create new ArrayBuffer and add data in specific order.
            ArrayBuffer arrayBuffer = new ArrayBuffer(String.Length * 2)
                .AddByte((byte)DataType)
                .AddString(String);

            bytes = arrayBuffer.Bytes;
            return arrayBuffer.Length;
        }

        public override string GetDescription()
        {
            return "[TextDataObject] String: " + String;
        }



        /// <summary>
        /// Factory responsible for creating TextDataObjects. Allows creating from default/raw
        ///  data or by deserializing from byte array.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Attemps to create and return a new TextDataObject from default/raw data. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="str"> Text to be contained in the TextDataObject. Must not be null, empty, or Length > 127. </param>
            /// <returns> The newly created TextDataObject, or null if unsuccessful (invalid argument). </returns>
            public static TextDataObject? CreateFromDefault(string str)
            {
                // Validate argument data. String should not be null or empty.
                if (string.IsNullOrEmpty(str) || str.Length > 127)
                {
                    return null;
                }

                string temp = NetStatics.FormatStringForSend(str);
                return new TextDataObject(temp);
            }

            /// <summary>
            /// Attempts to create and return a new TextDataObject by deserializing from a byte[]. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="bytes"> Raw byte[] containing serialized TextDataObject data. Must correctly formatted. </param>
            /// <param name="length"> Length of data in byte[] (allocated size with Array.Length is probably longer). </param>
            /// <returns> The newly created TextDataObject, or null if unsuccessful (argument byte[] is malformed). </returns>
            public static TextDataObject? CreateFromDeserialize(byte[] bytes, int length)
            {
                // Validate argument data. 1 byte for DataType, must be at between 1 - 254 bytes for string data.
                if (length < 2 || length > 255)
                {
                    return null;
                }

                // Create ArrayBuffer from incoming data, then read data in reverse order.
                ArrayBuffer arrayBuffer = new ArrayBuffer(bytes, length);

                string str = arrayBuffer.ReadString();

                return new TextDataObject(str);
            }
        }
    }
}
