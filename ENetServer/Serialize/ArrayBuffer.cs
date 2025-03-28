using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Serialize
{
    
    public class ArrayBuffer
    {
        public byte[] Bytes { get; private set; }
        public int Length { get; private set; }

        public ArrayBuffer(int capacity)
        {
            Bytes = new byte[capacity];
            Length = 0;
        }

        public ArrayBuffer(byte[] bytes, int length)
        {
            Bytes = bytes;
            Length = length;
        }

        private ArrayBuffer FromCapacity(int capacity)
        {
            Bytes = new byte[capacity];
            Length = 0;
            return this;
        }

        private ArrayBuffer FromBytes(byte[] bytes, int length)
        {
            Bytes = bytes;
            Length = length;
            return this;
        }



        public ArrayBuffer AddByte(byte value)
        {
            // Length will equal next empty index, so set element at Length then increment.
            Bytes[Length] = value;
            Length++;

            return this;
        }

        public byte ReadByte()
        {
            // Move length index left one byte, then directly return that byte.
            Length--;
            return Bytes[Length];
        }

        public ArrayBuffer AddUInt(uint value)
        {
            // Convert uint to array of size 4, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 4);
            Length += 4;

            return this;
        }

        public uint ReadUInt()
        {
            // Move length index left 4 bytes, then directly return uint from those bytes.
            Length -= 4;
            return BitConverter.ToUInt32(Bytes, Length);
        }

        public ArrayBuffer AddDouble(double value)
        {
            // Convert uint to array of size 8, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 8);
            Length += 8;

            return this;
        }

        public double ReadDouble()
        {
            // Move length index left 8 bytes, then directly return double from those bytes.
            Length -= 8;
            return BitConverter.ToDouble(Bytes, Length);
        }

        public ArrayBuffer AddString(string value)
        {
            // IMPORTANT: String argument should be verified length <= 127 before calling this method.

            //if (string.IsNullOrEmpty(value) || value.Length > 127)
            //{
            //    throw new ArgumentException("Argument string cannot be longer than 127 characters in length.");
            //}

            // Convert string into dynamic-sized byte[], then copy directly into main byte[].
            byte[] temp = Encoding.UTF8.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, temp.Length);
            Length += temp.Length;

            // Add byte for string length immediately after string.
            AddByte((byte)temp.Length);

            return this;
        }

        public string ReadString()
        {
            // Read length of bytes used by the string from the byte immediately after string.
            byte strLen = ReadByte();

            // Move length index left by strLen bytes, then directly return string from those bytes.
            Length -= strLen;
            return Encoding.UTF8.GetString(Bytes, Length, strLen);
        }



        /// <summary>
        /// NON-THREAD SAFE pool of one ArrayBuffer object. This will return a static
        ///  instance of an ArrayPool, and should only be used on the same thread.
        /// </summary>
        public static class Pool
        {
            private static readonly ArrayBuffer arrayBuffer = new(1024);

            public static ArrayBuffer GetEmpty(int capacity)
            {
                // If already exists, reset data and length and set capacity.
                return arrayBuffer.FromCapacity(capacity);
            }

            public static ArrayBuffer GetFromBytes(byte[] bytes, int length)
            {
                // If already exists, set data and length to passed-in values.
                return arrayBuffer.FromBytes(bytes, length);
            }
        }

    }
}
