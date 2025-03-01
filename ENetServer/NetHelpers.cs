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
        public enum SendType { MESSAGE_ONE, MESSAGE_ALL, MESSAGE_ALLEXCEPT, DISCONNECT_ONE, DISCONNECT_ALL }
        internal enum RecvType { CONNECT, DISCONNECT, TIMEOUT, MESSAGE }
        public enum DataType : byte { NONE, TEXT, TRANSFORM }
        // NOTE: Adding a new DataType requires modifications in:
        //  1. Here! Make a new DataType.
        //  2. Within GameOutDataObject in TWO places: First within CheckIsValid() to verify data is not
        //      malformed, second by creating a new static template method (i.e. MakeDataTypeMethodName()).
        //  3. Within Serializer in TWO places: First within SerializeGameOutObject() to properly handle
        //      the type of data being serialized, and second within DeserializeGameInObject() to do the
        //      same but in reverse.



        #region String and Packet Helpers

        /// <summary>
        /// Converts byte[] from received packet and converts to a string.
        /// IMPORTANT: Removes null terminator \0 from the end of the string.
        /// </summary>
        /// <param name="bytes"> Byte array containing raw data from received message. </param>
        /// <returns> The formatted string without the null terminator. </returns>
        internal static string FormatStringFromReceive(byte[] bytes)
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
        internal static string FormatStringForSend(string message)
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
        internal static byte[] CreateByteArrayFromUTF8String(string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            return bytes;
        }

        #endregion

        #region Original -> Byte helpers

        internal static byte[] GetBytes(int[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(int)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        internal static byte[] GetBytes(uint[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(uint)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        internal static byte[] GetBytes(float[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        internal static byte[] GetBytes(double[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        #endregion

        #region Byte -> Original helpers

        internal static int[] GetInts(byte[] values)
        {
            int[] ints = new int[values.Length / sizeof(int)];
            Buffer.BlockCopy(values, 0, ints, 0, values.Length);
            return ints;
        }

        internal static uint[] GetUInts(byte[] values)
        {
            uint[] uints = new uint[values.Length / sizeof(uint)];
            Buffer.BlockCopy(values, 0, uints, 0, values.Length);
            return uints;
        }

        internal static float[] GetFloats(byte[] values)
        {
            float[] floats = new float[values.Length / sizeof(float)];
            Buffer.BlockCopy(values, 0, floats, 0, values.Length);
            return floats;
        }

        internal static double[] GetDoubles(byte[] values)
        {
            double[] doubles = new double[values.Length / sizeof(double)];
            Buffer.BlockCopy(values, 0, doubles, 0, values.Length);
            return doubles;
        }

        #endregion

        #region Array helpers

        internal static byte[] MergeByteArrays(byte[] first, byte[] second)
        {
            byte[] bytes = new byte[first.Length + second.Length];

            // Copy first array directly into bytes, then copy second array starting at end of first.
            Array.Copy(first, bytes, first.Length);
            Array.Copy(second, 0, bytes, first.Length, second.Length);

            return bytes;
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
