using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetHpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            SocketServer socketServer = new SocketServer();
            socketServer.Init();
            //var server = new SLW.RtspServer.AsyncSocketServer();
            //socketServer.OnReceiveData += server.OnSocketSendEvent;
            //socketServer.OnReceiveData += server.receive;

            Task.Run(() =>
            {
                while (true)
                {
                    //socketServer.SendToAll(Encoding.Default.GetBytes("Hello! This is the message that server sends to all clients,Hello! This is the message that server sends to all clients "));
                    Thread.Sleep(50);
                }
            });

            string c;
            while ((c = Console.ReadKey().KeyChar.ToString()) != null)
            {
                switch (c)
                {
                    case "1":
                        socketServer.SendToAll(Encoding.Default.GetBytes("Hello! This is the message that server sends to all clients"));
                        Console.WriteLine();
                        break;
                    case "2":
                        break;
                }
            }
        }
    }
}
