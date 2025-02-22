using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    /// <summary>
    /// Static class containing helper methods for handling WebSocket operations (connection, frames, etc.).
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Holds data for an encoded message frame.
        /// </summary>
        public struct SFrameMaskData
        {
            public int DataLength, KeyIndex, TotalLength;
            public EOpcodeType Opcode;

            /// <summary>
            /// Creates a struct containing data pertaining to a frame. Contains only data like sizes, indices,
            ///  and opcode type, does NOT include message contents.
            /// </summary>
            /// <param name="dataLength"> Length of the data as denoted at the beginning of the frame </param>
            /// <param name="keyIndex"> Index where the key is located (after the location data index) </param>
            /// <param name="totalLength"> Total length of the frame (will be data length + bytes required to pass data length values) </param>
            /// <param name="opcode"> The frame's opcode </param>
            public SFrameMaskData(int dataLength, int keyIndex, int totalLength, EOpcodeType opcode)
            {
                DataLength = dataLength;
                KeyIndex = keyIndex;
                TotalLength = totalLength;
                Opcode = opcode;
            }
        }

        /// <summary>
        /// Enum for WebSocket opcode types.
        /// </summary>
        public enum EOpcodeType
        {
            /* Denotes a continuation code */
            Fragment = 0,

            /* Denotes a text code */
            Text = 1,

            /* Denotes a binary code */
            Binary = 2,

            /* Denotes a closed connection */
            ClosedConnection = 8,

            /* Denotes a ping*/
            Ping = 9,

            /* Denotes a pong */
            Pong = 10
        }



        /// <summary>
        /// Gets frame data from an encoded WebSocket message (data only, not message content).
        /// </summary>
        /// <param name="data"> Byte array to pull frame data from </param>
        /// <returns> A new SFrameMaskData struct object containing frame data </returns>
        public static SFrameMaskData GetFrameData(byte[] data)
        {
            // Get the opcode of the frame.
            EOpcodeType opcode = GetFrameOpcode(data);

            // If the payload length (contained at index 1) is between 0 and 125, then it
            //  is the length of the message.
            if (data[1] - 128 <= 125)
            {
                int dataLength = (data[1] - 128);
                return new SFrameMaskData(dataLength, 2, dataLength + 6, opcode);   //6 bytes used to store length
            }

            // If the payload length is 126, the following 2 bytes contain the message length.
            if (data[1] - 128 == 126)
            {
                // Combine the bytes at the next two indices to get the length.
                int dataLength = BitConverter.ToInt16([data[3], data[2]], 0);
                return new SFrameMaskData(dataLength, 4, dataLength + 8, opcode);   //8 bytes used to store length
            }

            // If the payload length is 127, the following 8 bytes contain the message length.
            if (data[1] - 128 == 127)
            {
                // Store the following eight bytes in a temporary array, which is combined below.
                byte[] combine = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    combine[i] = data[i + 2];
                }

                // Combine all eight bytes in the temporary array to get the length.
                //int dataLength = (int)BitConverter.ToInt64(new byte[] { Data[9], Data[8], Data[7], Data[6], Data[5], Data[4], Data[3], Data[2] }, 0);
                int dataLength = (int)BitConverter.ToInt64(combine, 0);
                return new SFrameMaskData(dataLength, 10, dataLength + 14, opcode); //14 bytes used to store length
            }

            // Here will only be reached if there is an error with the payload length stored at
            //  index 1 of the byte array. If there is an error, return effectively empty struct.
            return new SFrameMaskData(0, 0, 0, 0);
        }

        /// <summary>
        /// Gets the opcode of the passed-in frame byte array.
        /// </summary>
        /// <param name="frame"> The frame to get the opcode from </param>
        /// <returns> The opcode of the frame </returns>
        public static EOpcodeType GetFrameOpcode(byte[] frame)
        {
            // Because the first bit is always 1 for client-to-server messages, subtracting 128
            //  from the first byte will get rid of the MASK bit which we already know is 1 to
            //  produce the frame's opcode.
            // Subtract 128 not 256 because a byte is a SIGNED 8-bit integer.
            return (EOpcodeType)frame[0] - 128;
        }

        /// <summary>
        /// Gets the decoded frame data (as a string) from the given byte array.
        /// </summary>
        /// <param name="data"> The byte array to decode into string form </param>
        /// <returns> The decoded data as a string (as it was sent from the client) </returns>
        public static string GetDataFromFrame(byte[] data)
        {
            // Get the frame data (data values, not message content).
            SFrameMaskData frameData = GetFrameData(data);

            // Get the decode frame key from the frame data, which should always be 4 bytes long.
            byte[] decodeKey = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                // The decode frame key start point is stored in the SFrameMaskData struct object.
                decodeKey[i] = data[frameData.KeyIndex + i];
            }

            // The start index for the frame's actual data begins 4 bytes after the decode key.
            int dataIndex = frameData.KeyIndex + 4;
            int count = 0;

            // Decode the data using the key, starting at dataIndex and ending at the frame's total length.
            for (int i = dataIndex; i < frameData.TotalLength; i++)
            {
                data[i] = (byte)(data[i] ^ decodeKey[count % 4]);
                count++;
            }

            // Return the decoded message as a string.
            return Encoding.Default.GetString(data, dataIndex, frameData.DataLength);
        }

        /// <summary>
        /// Checks if a byte array buffer is valid (checks nullity and size).
        /// </summary>
        /// <param name="buffer"> The byte array to check </param>
        /// <returns> Whether the byte array buffer is valid </returns>
        public static bool GetIsBufferValid(ref byte[] buffer)
        {
            if (buffer == null) return false;
            if (buffer.Length <= 0) return false;

            return true;
        }

        /// <summary>
        /// Gets an encoded WebSocket frame (as a byte array) from a raw string, to be sent to a client.
        /// </summary>
        /// <param name="message"> The message string to encode into the frame </param>
        /// <param name="opcode"> The opcode of the frame (default value Text) </param>
        /// <returns> The passed-in message string as a byte array in the form of a WebSocket frame </returns>
        public static byte[] GetFrameFromString(string message, EOpcodeType opcode = EOpcodeType.Text)
        {
            byte[] response;
            byte[] bytesRaw = Encoding.Default.GetBytes(message);
            byte[] frame = new byte[10];
            int length = bytesRaw.Length;
            int indexStartRawData;

            // Set the first index to the opcode value (add 128 because signed byte).
            frame[0] = (byte)(128 + (int)opcode);

            // If length is 125, the length is stored in a single byte at index 1.
            if (length <= 125)
            {
                frame[1] = (byte)length;
                indexStartRawData = 2;  // Starts after bytes containing length data
            }
            // If length is 126, the length is stored in 2 bytes at indices 2-3.
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (byte)126;
                frame[2] = (byte)((length >> 8) & 255);
                frame[3] = (byte)(length & 255);
                indexStartRawData = 4;
            }
            // Else length is 127, so the length is stored in 8 bytes at indices 2-9.
            else
            {
                frame[1] = (byte)127;
                frame[2] = (byte)((length >> 56) & 255);
                frame[3] = (byte)((length >> 48) & 255);
                frame[4] = (byte)((length >> 40) & 255);
                frame[5] = (byte)((length >> 32) & 255);
                frame[6] = (byte)((length >> 24) & 255);
                frame[7] = (byte)((length >> 16) & 255);
                frame[8] = (byte)((length >> 8) & 255);
                frame[9] = (byte)(length & 255);

                indexStartRawData = 10;
            }

            // Create response byte array, which is the passed-in byte array length PLUS
            //  the bytes needed for the frame data.
            response = new byte[indexStartRawData + length];
            int i, reponseIdx = 0;

            // Add the frame bytes to the reponse, starting at index 0 and stopping before the raw data start index.
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            // Add the data bytes to the response, starting immediately after the last index of the
            //  raw data (stored within responseIdx counter).
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }

        /// <summary>
        /// Hash a WebSocket Upgrade request key with SHA1 to get the Accept response key.
        /// </summary>
        /// <param name="requestKey"> The Upgrade request key </param>
        /// <returns> The salted and hashed Accept key in the expected format </returns>
        public static string HashKey(string requestKey)
        {
            // Append (salt) the WebSocket protocol key (used for Accept reponse) to the passed-in request key.
            const string handshakeKey = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            string longKey = requestKey + handshakeKey;

            // Hash the salted key with SHA1.
            byte[] hashBytes = SHA1.HashData(Encoding.ASCII.GetBytes(longKey));

            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Gets the WebSocket handshake Accept response string to send to the WebSocket client, which
        ///  includes the generated SHA1 hashed Accept key.
        /// </summary>
        /// <param name="acceptKey"> The SHA1 hashed Accept key required in the response </param>
        /// <returns> The generated Accept reponse string, in CRLF format (\r\n newlines) </returns>
        public static string GetHandshakeResponse(string acceptKey)
        {

            // This follows a very specific protocol and must use CRLF formatting as the standard.
            // Ensure that \r\n is used for newlines, not just \n.
            return string.Format("HTTP/1.1 101 Switching Protocols\r\nUpgrade: WebSocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {0}\r\n\r\n", acceptKey);
        }

        /// <summary>
        /// Gets the WebSocket handshake Upgrade key from the client's initial HTTP request.
        /// </summary>
        /// <param name="httpRequest"> The HTTP request string containing the Upgrade key </param>
        /// <returns> The request Upgrade key as an isolated string </returns>
        public static string GetHandshakeRequestKey(string httpRequest)
        {
            int keyStart = httpRequest.IndexOf("Sec-WebSocket-Key: ") + 19;
            string key = "";

            // Key will be exactly 24 characters long.
            for (int i = keyStart; i < (keyStart + 24); i++)
            {
                key += httpRequest[i];
            }

            return key;
        }

        /// <summary>
        /// Creates a random GUID. Length and prefix parameters are both optional; will generate
        ///  a random non-prefixed GUID of length 16 if no arguments are passed.
        /// </summary>
        /// <param name="prefix"> (Optional, default "") The prefix of the GUID </param>
        /// <param name="length"> (Optional, default 16) The length of the GUID to generate </param>
        /// <returns> The randomly generated GUID (ex. prefix-XXXXXXXXXXXXXXXX) </returns>
        public static string CreateGuid(string prefix = "", int length = 16)
        {
            string final = "";
            string ids = "0123456789abcdefghijklmnopqrstuvwxyz";

            Random random = new();

            // Loops for each character in GUID, appending a character randomly selected from 'ids'.
            for (short i = 0; i < length; i++)
            {
                final += ids[random.Next(0, ids.Length)];
            }

            // If prefix param is empty string, return the GUID without a prefix.
            if (prefix == "")
            {
                return final;
            }

            // Else return the GUID with the prefix prepended with a hyphen (-) in-between.
            return string.Format("{0}-{1}", prefix, final);
        }
    }
}
