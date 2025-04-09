using ENet;
using ENetServer.Network.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network
{
    internal class Validator
    {
        private Dictionary<string, BlacklistData> BlacklistMap { get; } = new();
        private Dictionary<string, ValidationData> ValidationMap { get; } = new();
        private Dictionary<string, ValidationData> OutgoingTokens { get; } = new(); // for login tokens we send

        public Validator(bool isServer)
        {
            // TODO: REMOVE TEMPORARY POPULATE MAPS WITH DATA
            if (isServer)
            {
                // Client sends token starting with 0.
                ValidationMap["127.0.0.1:8888"] = new ValidationData("127.0.0.1", 8888, "0f8fad5bd9cb469fa16570867728950e");
                ValidationMap["127.0.0.1:8889"] = new ValidationData("127.0.0.1", 8889, "0f8fad5bd9cb469fa16570867728950e");
                ValidationMap["127.0.0.1:8890"] = new ValidationData("127.0.0.1", 8890, "0f8fad5bd9cb469fa16570867728950e");

                // Server sends token starting with 1.
                ValidationMap["127.0.0.1:7777"] = new ValidationData("127.0.0.1", 7777, "1f8fad5bd9cb469fa16570867728950e");
                ValidationMap["127.0.0.1:7778"] = new ValidationData("127.0.0.1", 7778, "1f8fad5bd9cb469fa16570867728950e");
                ValidationMap["127.0.0.1:7779"] = new ValidationData("127.0.0.1", 7779, "1f8fad5bd9cb469fa16570867728950e");

                OutgoingTokens["127.0.0.1:7777"] = new ValidationData("127.0.0.1", 7777, "1f8fad5bd9cb469fa16570867728950e");
                OutgoingTokens["127.0.0.1:7778"] = new ValidationData("127.0.0.1", 7778, "1f8fad5bd9cb469fa16570867728950e");
                OutgoingTokens["127.0.0.1:7779"] = new ValidationData("127.0.0.1", 7779, "1f8fad5bd9cb469fa16570867728950e");
            }
            else
            {
                // Client sends token starting with 0.
                OutgoingTokens["127.0.0.1:7777"] = new ValidationData("127.0.0.1", 8888, "0f8fad5bd9cb469fa16570867728950e");
                OutgoingTokens["127.0.0.1:7778"] = new ValidationData("127.0.0.1", 8889, "0f8fad5bd9cb469fa16570867728950e");
                OutgoingTokens["127.0.0.1:7779"] = new ValidationData("127.0.0.1", 8890, "0f8fad5bd9cb469fa16570867728950e");
            }
            
        }



        public bool AddIncomingToken(string ip, ushort port, string token)
        {
            string key = NetStatics.GetAddressString(ip, port);
            ValidationMap[key] = new ValidationData(ip, port, token);
            return true;
        }

        public bool AddOutgoingToken(string ip, ushort port, string token)
        {
            string key = NetStatics.GetAddressString(ip, port);
            OutgoingTokens[key] = new ValidationData(ip, port, token);
            return true;
        }



        public bool GetChecksumForConnectRequest(string ip, ushort port, out uint checksum)
        {
            string key = NetStatics.GetAddressString(ip, port);
            if (OutgoingTokens.TryGetValue(key, out ValidationData? validationData))
            {
                checksum = validationData.Checksum;
                return true;
            }

            checksum = 0u;
            return false;
        }

        public bool GetTokenForOutgoingConnect(string ip, ushort port, [NotNullWhen(true)] out string? token)
        {
            string key = NetStatics.GetAddressString(ip, port);
            if (OutgoingTokens.TryGetValue(key, out ValidationData? validationData))
            //if (OutgoingTokens.Remove(key, out ValidationData? validationData))   // USE Remove() LATER
            {
                token = validationData.LoginToken;
                return true;
            }

            token = string.Empty;
            return false;
        }

        public string GetValidationAckString()
        {
            // TODO: IMPLEMENT DYNAMIC TOKEN ACK STRING INSTEAD OF STRING LITERAL
            return NetStatics.FormatStringForSend("Login token validation successful.");
        }

        public string GetAckResponseString()
        {
            // TODO: IMPLEMENT DYNAMIC RESPONSE STRING INSTEAD OF STRING LITERAL
            return NetStatics.FormatStringForSend("Validation ACK received successfully.");
        }



        /// <summary>
        /// Compares the passed-in checksum uint (received over the network) with the
        ///  stored Checksum data for the associated Peer, if it exists.
        /// </summary>
        /// <param name="inChecksum"> Checksum uint received over the network, being compared. </param>
        /// <returns> Whether the stored (awaiting) and passed-in checksums match. </returns>
        public bool CompareChecksum(string ip, ushort port, uint inChecksum)
        {
            string key = NetStatics.GetAddressString(ip, port);
            if (ValidationMap.TryGetValue(key, out ValidationData? validationData))
            {
                if (validationData.CompareChecksum(inChecksum))
                {
                    return true;
                }
            }

            // Returns false if Peer not in ValidationMap OR fails checksum validation.
            return false;
        }

        /// <summary>
        /// Compares the passed-in login token string (received over the network) with
        ///  the stored LoginToken data for the associated Peer, if it exists.
        /// </summary>
        /// <param name="inLoginToken"> Login token string received over the network, being compared. </param>
        /// <returns> Whether the stored (awaiting) and passed-in login tokens match. </returns>
        public bool CompareLoginToken(string ip, ushort port, string inLoginToken)
        {
            string key = NetStatics.GetAddressString(ip, port);
            if (ValidationMap.TryGetValue(key, out ValidationData? validationData))
            //if (ValidationMap.Remove(key, out ValidationData? validationData))    // USE Remove() LATER
            {
                if (validationData.CompareLoginToken(inLoginToken))
                {
                    // If passes validation, remove from BlacklistMap entirely.
                    BlacklistMap.Remove(key);
                    return true;
                }
            }

            // Returns false if Peer not in ValidationMap OR fails login token validation.
            return false;
        }

        public bool CompareValidationAck(string ackString)
        {
            // TODO: COMPARE WITH DYNAMIC VALIDATION ACK STRING INSTEAD OF STRING LITERAL
            return ackString.Equals("Login token validation successful.");
        }

        public bool CompareAckResponse(string response)
        {
            // TODO: COMPARE WITH DYNAMIC ACK RESPONSE STRING INSTEAD OF STRING LITERAL
            return response.Equals("Validation ACK received successfully.");
        }



        public bool IsPeerBlacklisted(string ip, ushort port)
        {
            string key = NetStatics.GetAddressString(ip, port);
            if (BlacklistMap.TryGetValue(key, out BlacklistData? blacklistData))
            {
                if (blacklistData.IsCurrentlyBlacklisted())
                {
                    return true;
                }
            }

            // Returns false if there is no entry for this Peer in the BlacklistMap.
            return false;
        }

        public void BlacklistPeer(string ip, ushort port)
        {
            string key = NetStatics.GetAddressString(ip, port);
            if (BlacklistMap.TryGetValue(key, out BlacklistData? blacklistData))
            {
                blacklistData.Reblacklist();
            }
            else
            {
                BlacklistMap[key] = new BlacklistData(ip, port);
            }
        }
    }
}
