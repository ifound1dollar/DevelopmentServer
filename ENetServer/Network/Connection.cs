using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Management
{
    public class Connection
    {
        public uint ID { get; }
        public string IP { get; }
        public ushort Port { get; }
        public DateTime ConnectTime { get; }

        public Connection(uint id, string ip, ushort port)
        {
            ID = id;
            IP = ip;
            Port = port;

            ConnectTime = DateTime.Now;
        }
    }
}
