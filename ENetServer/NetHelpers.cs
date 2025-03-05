using ENet;
using System.Collections;
using System;
using System.Diagnostics;
using System.Text;
using static ENetServer.NetworkManager;

namespace ENetServer
{
    /// <summary>
    /// Static class containing enums and methods used for networking operations.
    /// </summary>
    public static class NetHelpers
    {
        public enum SendType { None, Disconnect_One, Disconnect_All, Message_One, Message_All, Message_AllExcept }
        public enum RecvType { None, Connect, Disconnect, Timeout, Message }



        #region String and Packet Helpers

        /// <summary>
        /// Converts byte[] from received packet and converts to a string.
        /// IMPORTANT: Removes null terminator \0 from the end of the string.
        /// </summary>
        /// <param name="bytes"> Byte array containing raw data from received message. </param>
        /// <returns> The formatted string without the null terminator. </returns>
        public static string FormatStringFromReceive(byte[] bytes)
        {
            // Convert passed-in byte[] into UTF8 string.
            string message = Encoding.UTF8.GetString(bytes);

            // Remove null terminator from end of string.
            message = message.Trim('\0');

            return message;
        }

        /// <summary>
        /// Formats the passed-in string into universal UTF8 format, notably appending the null terminator \0.
        /// </summary>
        /// <param name="message"> Message to format. </param>
        /// <returns> The formatted string with the null terminator \0 as the final character. </returns>
        public static string FormatStringForSend(string message)
        {
            // Append the null terminator \0 to the end of the string.
            // C# does not use the null terminator, so strings created here are not suffixed with \0.
            // C++ requires the null terminator, so it must be appended to the end of the string.
            message += "\0";    // SHOULD BE GUARANTEED TO NOT YET HAVE THE CHARACTER WHEN THIS METHOD IS CALLED

            return message;
        }

        /// <summary>
        /// Creates a byte[] from the passed-in string, which must be in UTF8 format and include the null terminator \0.
        /// </summary>
        /// <param name="message"> String to convert to byte[]. Must include null terminator \0. </param>
        /// <returns> The byte[] generated from the string. </returns>
        //public static byte[] CreateByteArrayFromUTF8String(string message)
        //{
        //    byte[] bytes = Encoding.UTF8.GetBytes(message);
        //    return bytes;
        //}

        #endregion

        #region Original -> Byte helpers

        public static byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(value);
        }
        public static byte[] GetBytes(int[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(int)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] GetBytes(uint value)
        {
            return BitConverter.GetBytes(value);
        }
        public static byte[] GetBytes(uint[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(uint)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] GetBytes(float value)
        {
            return BitConverter.GetBytes(value);
        }
        public static byte[] GetBytes(float[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] GetBytes(double value)
        {
            return BitConverter.GetBytes(value);
        }
        public static byte[] GetBytes(double[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] GetBytes(string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            return bytes;
        }

        #endregion

        #region Byte -> Original helpers

        public static int GetInt(byte[] values, int startIndex)
        {
            return BitConverter.ToInt32(values, startIndex);
        }
        public static int[] GetInts(byte[] values, int startIndex, int length)
        {
            int[] ints = new int[values.Length / sizeof(int)];
            Buffer.BlockCopy(values, startIndex, ints, 0, length);
            return ints;
        }

        public static uint GetUInt(byte[] values, int startIndex)
        {
            return BitConverter.ToUInt32(values, startIndex);   // Always reads exactly four bytes
        }
        public static uint[] GetUInts(byte[] values, int startIndex, int length)
        {
            uint[] uints = new uint[values.Length / sizeof(uint)];
            Buffer.BlockCopy(values, startIndex, uints, 0, length);
            return uints;
        }

        public static float GetFloat(byte[] values, int startIndex)
        {
            return BitConverter.ToSingle(values, startIndex);   // Single is float
        }
        public static float[] GetFloats(byte[] values, int startIndex, int length)
        {
            float[] floats = new float[values.Length / sizeof(float)];
            Buffer.BlockCopy(values, startIndex, floats, 0, length);
            return floats;
        }

        public static double GetDouble(byte[] values, int startIndex)
        {
            return BitConverter.ToDouble(values, startIndex);
        }
        public static double[] GetDoubles(byte[] values, int startIndex, int length)
        {
            double[] doubles = new double[values.Length / sizeof(double)];
            Buffer.BlockCopy(values, startIndex, doubles, 0, length);
            return doubles;
        }

        public static string GetString(byte[] values, int startIndex, int length)
        {
            return Encoding.UTF8.GetString(values, startIndex, length);
        }

        #endregion

        #region Array helpers

        /// <summary>
        /// Merges two byte arrays using Array.Copy().
        /// </summary>
        /// <param name="first"> Array to come first in merged byte[]. </param>
        /// <param name="second"> Array to come second in merged byte[]. </param>
        /// <returns> The resulting merged byte[]. </returns>
        public static byte[] MergeTwoByteArrays(byte[] first, byte[] second)
        {
            byte[] bytes = new byte[first.Length + second.Length];

            // Copy first array directly into bytes, then copy second array starting at end of first.
            Array.Copy(first, bytes, first.Length);
            Array.Copy(second, 0, bytes, first.Length, second.Length);

            return bytes;
        }

        /// <summary>
        /// Merges multiple byte arrays in order (effectively concatenating) using Array.Copy().
        /// </summary>
        /// <param name="bytes"> A series of byte arrays, in order, which will be merged into one array. </param>
        /// <returns> The resulting merged byte[]. </returns>
        public static byte[] ConcatByteArrays(params byte[][] bytes)
        {
            int position = 0;

            // Use selector to sum the length of each input array, then use to initialize outputArray.
            byte[] outputArray = new byte[bytes.Sum(a => a.Length)];

            // Loop over each input byte[] and copy to outputArray.
            foreach (byte[] curr in bytes)
            {
                // Copy entirety of curr into outputArray starting at 'position' index if outputArray.
                Array.Copy(curr, 0, outputArray, position, curr.Length);
                position += curr.Length;    // Move position to end of just-added 'curr' array.
            }
            return outputArray;
        }

        #endregion





        private static void DoFixedIntervalTick()
        {
            double tickIntervalExact = 1000.0d / 30.0d;     // 30 per second
            int tickInterval = (int)Math.Round(tickIntervalExact);
            int sleepTime;
            Stopwatch stopwatch = new();

            // Will continue looping until 'shouldExit' is set to true, which should be done via SetShouldExit().
            while (/*!shouldExit*/ true)
            {
                // Restart timer and actually perform tick operations.
                stopwatch.Restart();
                //TickService();



                /* ----- BELOW: WAIT FOR FIXED DURATION UNTIL NEXT TICK ----- */

                // Sleep method execution takes on average another ~1ms, so sleep for 2ms less.
                // Then, block for remaining duration (blocking causes high CPU utilization).
                sleepTime = tickInterval - (int)stopwatch.ElapsedMilliseconds - 2;

                // Only sleep if wait time is more than 2ms (not worth it otherwise).
                if (sleepTime > 2)
                {
                    Thread.Sleep(sleepTime);
                }

                // Manual block for remaining exact milliseconds (high CPU utilization).
                while (stopwatch.Elapsed.TotalMilliseconds < tickIntervalExact)
                {
                    // Block
                }

                //TEMP
                //Console.WriteLine(stopwatch.Elapsed.TotalMilliseconds.ToString());
                //TEMP
            }
        }

        // DO NOT USE THIS METHOD - THIS MAY CAUSE A MEMORY LEAK BECAUSE THIS PACKET IS NOT MANUALLY DISPOSED BY ENET.
        /// <summary>
        /// Creates a Packet object from the passed-in string. String should be compatible with UTF8.
        /// </summary>
        /// <param name="message"> String to convert to UTF8 byte[] and add to packet. </param>
        /// <returns> The created packet from the passed-in string. </returns>
        private static Packet CreatePacketFromUTF8String(string message)
        {
            // Create packet with input string, which should be UTF8 encoded.
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            Packet packet = default;
            packet.Create(bytes);

            return packet;
        }


    }
}
