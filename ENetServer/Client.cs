using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer
{
    public class Client
    {
        public Client(uint id, string ip, ushort port)
        {
            ID = id;
            IP = ip;
            Port = port;

            ConnectTime = DateTime.Now;
        }

        public uint ID { get; private set; }
        public string IP { get; private set; }
        public ushort Port { get; private set; }
        public DateTime ConnectTime { get; private set; }
    }
}
