using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network
{
    public record BlacklistData
    {
        public string IP { get; }
        public ushort Port { get; }
        private int Counter { get; set; }
        private DateTime Expiration { get; set; }

        public BlacklistData(string ip, ushort port)
        {
            IP = ip;
            Port = port;

            Counter = 1;
            Expiration = DateTime.UtcNow.AddMinutes(5.0d);  // Initial blacklist duration 5 minutes
        }

        /// <summary>
        /// Should be called when a remote host fails another validation and must be re-blacklisted.
        /// Increments counter and sets blacklist expiration time accordingly (exponential duration
        ///  based on counter).
        /// </summary>
        public void Reblacklist()
        {
            Counter++;
            Expiration = DateTime.UtcNow.AddMinutes(Math.Pow(5.0d, Counter));
        }

        /// <summary>
        /// Gets whether this BlacklistData is still in effect, meaning the Expiration has not yet
        ///  been reached.
        /// </summary>
        /// <returns> Whether the Peer referred to by this BlacklistData object is still blacklisted. </returns>
        public bool IsCurrentlyBlacklisted()
        {
            // Returns < 0 if first is before second, meaning it is before expiration time.
            bool isBlacklisted = DateTime.Compare(DateTime.UtcNow, Expiration) < 0;
            return isBlacklisted;
        }
    }
}
