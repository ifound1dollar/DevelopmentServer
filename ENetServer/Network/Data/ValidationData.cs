using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network.Data
{
    public record ValidationData
    {
        public string IP { get; }
        public ushort Port { get; }
        public DateTime Expiration { get; }
        public uint Checksum { get; }
        public string LoginToken { get; }

        public ValidationData(string ip, ushort port, string loginToken)
        {
            IP = ip;
            Port = port;
            LoginToken = loginToken;

            Checksum = CalculateChecksum(loginToken);
            Expiration = DateTime.UtcNow.AddMinutes(5);
        }



        /// <summary>
        /// Compares the passed-in checksum uint (received over the network) with the
        ///  stored Checksum data.
        /// </summary>
        /// <param name="inChecksum"> Checksum uint received over the network, being compared. </param>
        /// <returns> Whether the stored and passed-in checksums match. </returns>
        public bool CompareChecksum(uint inChecksum)
        {
            // If expired, return false (returns > 0 if first is AFTER second).
            if (DateTime.Compare(DateTime.UtcNow, Expiration) > 0)
            {
                return false;
            }

            // Else return whether the checksums match.
            return Checksum == inChecksum;
        }

        /// <summary>
        /// Compares the passed-in login token string (received over the network) with
        ///  the stored LoginToken data.
        /// </summary>
        /// <param name="inLoginToken"> Login token string received over the network, being compared. </param>
        /// <returns> Whether the stored and passed-in login tokens match. </returns>
        public bool CompareLoginToken(string inLoginToken)
        {
            // If expired, return false (returns > 0 if first is AFTER second).
            if (DateTime.Compare(DateTime.UtcNow, Expiration) > 0)
            {
                return false;
            }

            // Else return whether the login tokens match.
            return LoginToken == inLoginToken;
        }

        /// <summary>
        /// Calculates an unsigned 32-bit checksum from a string of arbitrary size, using the
        ///  Knuth hashing algorithm.
        /// </summary>
        /// <param name="read"> The string to hash. </param>
        /// <returns> The passed-in string as a non-cryptographically-secure uint hash. </returns>
        private static uint CalculateChecksum(string read)
        {
            // See accepted comment here for details on Knuth hash:
            //  https://stackoverflow.com/questions/9545619/a-fast-hash-function-for-string-in-c-sharp

            uint hashedValue = 59;  // Should use a sufficiently large prime number in the future.
            for (int i = 0; i < read.Length; i++)
            {
                hashedValue += read[i];
                hashedValue *= 59;
            }
            return hashedValue;
        }
    }
}
