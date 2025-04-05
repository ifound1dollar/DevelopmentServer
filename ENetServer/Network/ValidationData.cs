using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network
{
    public readonly record struct ValidationData
    {
        public string IP { get; }
        public ushort Port { get; }
        private DateTime Expiration { get; }
        private uint Checksum { get; }
        private string LoginToken { get; }

        public ValidationData(string ip, ushort port, string loginToken)
        {
            IP = ip;
            Port = port;
            LoginToken = loginToken;

            Checksum = NetStatics.CalculateChecksum(loginToken);
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
            return (Checksum == inChecksum);
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
    }
}
