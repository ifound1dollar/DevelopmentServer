using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetServer.Network
{
    internal class Client
    {
        // IMPORTANT: CLIENT WILL ONLY ACTUALLY PERFORM SELF-DISCONNECT OPERATIONS AND SENDING
        //  MESSAGES TO THE SINGLE REMOTE HOST. IF THE CLIENT ATTEMPTS TO SEND ANY MESSAGE WITH
        //  A SENDTYPE THAT IS NOT DISCONNECT_ONE (AKA SELF) OR MESSAGE_ONE, IT WILL BE SIMPLY
        //  DISCARDED.
        // ALSO OBVIOUSLY WILL NOT HAVE A DICTIONARY FOR CONNECTIONS (ONLY REMOTE HOST CONNECTION)

        internal void DoNetReceiveTasks()
        {
            throw new NotImplementedException();
        }

        internal void DoNetSendTasks()
        {
            throw new NotImplementedException();
        }

        internal Address GetRemoteAddress()
        {
            throw new NotImplementedException();
        }

        internal void SetRemoteHostParameters()
        {
            throw new NotImplementedException();
        }

        internal void Start()
        {
            // Attempt to connect to remote host, check if returned Host is null to verify success.
            throw new NotImplementedException();
        }

        internal void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
