using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    public class UniversalMessageEventHandler : EventArgs
    {
        public Client Client {  get; private set; }
        public string Message { get; private set; }

        public UniversalMessageEventHandler(Client client, string message)
        {
            Client = client;
            Message = message;
        }
    }

    public class UniversalClientEventHandler : EventArgs
    {
        public Client Client { get; private set; }

        public UniversalClientEventHandler(Client client)
        {
            Client = client;
        }
    }
}
