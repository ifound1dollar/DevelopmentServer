﻿using ENetServer.Serialize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    public class TESTDataObject : GameDataObject
    {
        public string String { get; }

        private TESTDataObject(string str) : base(DataType.TEST)
        {
            String = str;
        }



        public override int Serialize(out byte[] bytes)
        {
            // Create new ArrayBuffer and add data in specific order.
            // Double length because 2 bytes per char (+ size byte immediately after).
            ArrayBuffer arrayBuffer = new ArrayBuffer(String.Length * 2)
                .AddByte((byte)DataType)
                .AddString(String);

            bytes = arrayBuffer.Bytes;
            return arrayBuffer.Length;
        }

        public override string GetDescription()
        {
            return "[TESTDataObject] DATA: " + String;
        }



        /// <summary>
        /// Factory responsible for creating TextDataObjects. Allows creating from default/raw
        ///  data or by deserializing from byte array.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Attemps to create and return a new TESTDataObject from default/raw data. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="str"> Text to be contained in the TESTDataObject. Must not be null, empty, or Length > 127. </param>
            /// <returns> The newly created TESTDataObject, or null if unsuccessful (invalid argument). </returns>
            public static TESTDataObject? CreateFromDefault(string str)
            {
                // Validate argument data. String should not be null or empty.
                if (string.IsNullOrEmpty(str) || str.Length > 127)
                {
                    return null;
                }

                string temp = NetStatics.FormatStringForSend(str);
                return new TESTDataObject(temp);
            }

            /// <summary>
            /// Attempts to create and return a new TESTDataObject by deserializing from a byte[]. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="bytes"> Raw byte[] containing serialized TESTDataObject data. Must correctly formatted. </param>
            /// <param name="length"> Length of data in byte[] (allocated size with Array.Length is probably longer). </param>
            /// <returns> The newly created TESTDataObject, or null if unsuccessful (argument byte[] is malformed). </returns>
            public static TESTDataObject? CreateFromDeserialize(byte[] bytes, int length)
            {
                // Validate argument data. 1 byte for DataType, must be at between 1 - 254 bytes for string data.
                if (length < 2 || length > 255)
                {
                    return null;
                }

                // Create ArrayBuffer from incoming data, then read data in reverse order.
                ArrayBuffer arrayBuffer = new ArrayBuffer(bytes, length);

                string str = arrayBuffer.ReadString();

                return new TESTDataObject(str);
            }
        }
    }
}
