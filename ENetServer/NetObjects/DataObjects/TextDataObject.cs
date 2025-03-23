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



        public override byte[] Serialize()
        {
            // Convert all data into raw byte arrays to be concatenated below.
            byte[] headerBytes = [(byte)DataType];
            byte[] stringBytes = NetStatics.GetBytes(String);

            // Concat all arrays together in this specific order, then return.
            byte[] bytes = NetStatics.ConcatByteArrays(headerBytes, stringBytes);
            return bytes;
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
            /// <param name="str"> Text to be contained in the TextDataObject. Must not be null or empty. </param>
            /// <returns> The newly created TextDataObject, or null if unsuccessful (invalid argument). </returns>
            public static TextDataObject? CreateFromDefault(string str)
            {
                // Validate argument data. String should not be null or empty.
                if (string.IsNullOrEmpty(str))
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
            /// <returns> The newly created TextDataObject, or null if unsuccessful (argument byte[] is malformed). </returns>
            public static TextDataObject? CreateFromDeserialize(byte[] bytes)
            {
                // Validate argument data. 1 byte for DataType, must be at least 1 byte for actual string data.
                if (bytes.Length < 2)
                {
                    return null;
                }

                string tempString = NetStatics.GetString(bytes, 0, bytes.Length);
                tempString = NetStatics.FormatStringForSend(tempString);
                return new TextDataObject(tempString);
            }
        }
    }
}
