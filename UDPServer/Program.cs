using System.Net;
using System.Net.Sockets;
using System.Text;
using ENet;

namespace UDPServer
{
    internal class Program
    {
        static UdpClient UdpClient {  get; set; }
        static IPEndPoint? RemoteIPEndPoint = new(IPAddress.Any, 54000); // Receive from any IP but only on port 54000

        static void Main(string[] args)
        {
            ENet.Library.Initialize();






            //Console.WriteLine("Hello, World!");

            try
            {
                UdpClient = new(54001);                 // Local port 54001
                UdpClient.Connect("127.0.0.1", 54000);  //Remote port 54000

                UdpClient.BeginReceive(MessageCallback, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to create UDP connection: " + ex.ToString());
                Environment.Exit(0);
            }

            //int counter = 0;
            //string sendString = "";
            //while (true)
            //{
            //    Thread.Sleep(100);
            //    sendString = counter.ToString();

            //    byte[] sendBytes = Encoding.UTF8.GetBytes(sendString);
            //    UdpClient.Send(sendBytes);
            //    Console.WriteLine("Sent message to client at 127.0.0.1:54000 : {0}", sendString);

            //    counter++;
            //}

            string? inputString = "";
            while (inputString != null && inputString.ToLower() != "exit")
            {
                inputString = Console.ReadLine();

                if (inputString != null)
                {
                    byte[] sendBytes = Encoding.UTF8.GetBytes(inputString);
                    int sentBytes = UdpClient.Send(sendBytes);
                    Console.WriteLine("Sent {0} bytes to {1}:{2}", sentBytes, "127.0.0.1", "54000");
                }
            }

            //IPAddress remoteAddress = IPAddress.Parse("127.0.0.1");







            ENet.Library.Deinitialize();
        }

        private static void MessageCallback(IAsyncResult asyncResult)
        {
            try
            {
                UdpClient.EndReceive(asyncResult, ref RemoteIPEndPoint);

                // Read the incomming message and decode into string
                byte[] bytesReceived = UdpClient.Receive(ref RemoteIPEndPoint);
                string returnData = Encoding.UTF8.GetString(bytesReceived);

                Console.WriteLine("Number of bytes received: " + bytesReceived.Length);
                foreach (byte b in bytesReceived)
                {
                    Console.Write(b + " ");
                }

                // Display received data
                Console.WriteLine("Received message: " + returnData);

                UdpClient.BeginReceive(MessageCallback, null);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Message received error: " + e.ToString());
            }
        }
    }
}
