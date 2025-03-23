using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network
{
    public class Connection
    {
        public uint ID { get; set; }
        public string IP { get; }
        public ushort Port { get; }
        public DateTime ConnectTime { get; set; }
        public bool IsServer { get; set; }

        public Connection(bool isServer)
        {
            IP = string.Empty;
            IsServer = isServer;
        }

        public Connection(string ip, ushort port, bool isServer)
        {
            IP = ip;
            Port = port;
            IsServer = isServer;
        }

        public Connection(uint id, string ip, ushort port, bool isServer)
        {
            ID = id;
            IP = ip;
            Port = port;
            IsServer = isServer;
        }

        public Connection(uint id, string ip, ushort port, DateTime connectTime, bool isServer)
        {
            ID = id;
            IP = ip;
            Port = port;
            ConnectTime = connectTime;
            IsServer = isServer;
        }
    }
}
