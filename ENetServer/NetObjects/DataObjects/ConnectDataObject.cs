using ENetServer.Serialize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.NetObjects.DataObjects
{
    public class ConnectDataObject : GameDataObject
    {
        private string IP { get; }
        private ushort Port { get; }
        private string LoginToken { get; }

        private ConnectDataObject(string ip, ushort port, string loginToken) : base(DataType.ConnectData)
        {
            IP = ip;
            Port = port;
            LoginToken = loginToken;
        }



        public override int Serialize(out byte[] bytes)
        {
            // Create new ArrayBuffer and add data in specific order.
            ArrayBuffer arrayBuffer = new ArrayBuffer(IP.Length + LoginToken.Length + 5)
                .AddByte((byte)DataType)
                .AddString(IP)
                .AddUShort(Port)
                .AddString(LoginToken);

            bytes = arrayBuffer.Bytes;
            return arrayBuffer.Length;
        }

        public override string GetDescription()
        {
            return ("[ConnectDataObject] IP: " + IP + ", Port: " + Port + ", Token: " + LoginToken);
        }



        /// <summary>
        /// Factory responsible for creating TextDataObjects. Allows creating from default/raw
        ///  data or by deserializing from byte array.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Attemps to create and return a new ConnectDataObject from default/raw data. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <returns> The newly created TextDataObject, or null if unsuccessful (invalid argument). </returns>
            public static ConnectDataObject? CreateFromDefault(string ip, ushort port, string token)
            {
                // Validate argument data. String should not be null or empty.
                if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(token))
                {
                    return null;
                }

                ip = NetStatics.FormatStringForSend(ip);
                token = NetStatics.FormatStringForSend(token);
                return new ConnectDataObject(ip, port, token);
            }

            /// <summary>
            /// Attempts to create and return a new ConnectDataObject by deserializing from a byte[]. User should
            ///  verify success immediately after calling this method.
            /// </summary>
            /// <param name="bytes"> Raw byte[] containing serialized ConnectDataObject data. Must correctly formatted. </param>
            /// <param name="length"> Length of data in byte[] (allocated size with Array.Length is probably longer). </param>
            /// <returns> The newly created ConnectDataObject, or null if unsuccessful (argument byte[] is malformed). </returns>
            public static ConnectDataObject? CreateFromDeserialize(byte[] bytes, int length)
            {
                // Validate argument data. 1 byte for DataType, must be at between 1 - 254 bytes for string data.
                if (length < 2 || length > 255)
                {
                    return null;
                }

                try
                {
                    // Create ArrayBuffer from incoming data, then read data in reverse order.
                    ArrayBuffer arrayBuffer = new ArrayBuffer(bytes, length);

                    string ip = arrayBuffer.ReadString();
                    ushort port = arrayBuffer.ReadUShort();
                    string token = arrayBuffer.ReadString();
                    ip = NetStatics.FormatStringFromReceive(ip);
                    token = NetStatics.FormatStringFromReceive(token);

                    return new ConnectDataObject(ip, port, token);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return null;
                }
            }
        }
    }
}
