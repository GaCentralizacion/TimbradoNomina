using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace CommonFunctions.Functions
{
    public class SocketManager
    {

        string output = "";

        public void CreateSocket(string IP, int Port)
        {            
            TcpListener tcpListener = null;
            IPAddress ipAddress = Dns.GetHostEntry(IP).AddressList[0];

            try
            {                
                tcpListener = new TcpListener(IPAddress.Any, Port);
                tcpListener.Start();
                output = "Socket Creado. \n Esperando conexion.";
                Console.WriteLine(output);
            }
            catch (Exception e)
            {
                output = "Error: " + e.ToString();
                Console.WriteLine(output);
            }
            while (true)
            {
                
                Thread.Sleep(10);                
                TcpClient tcpClient = tcpListener.AcceptTcpClient();
                
                byte[] bytes = new byte[256];
                NetworkStream stream = tcpClient.GetStream();
                stream.Read(bytes, 0, bytes.Length);
                SocketHelper helper = new SocketHelper();
                helper.processMsg(tcpClient, stream, bytes);

                Console.WriteLine("Se hizo una Peticion");

            }
        }





    }
}
