using System.Net.Sockets;
using NetHpServer.model.Enums;

namespace NetHpServer.model
{
    /// <summary>
    /// 当前Socket所处状态的参数
    /// </summary>
    public class SocketEventParam
    {
        public EN_SocketEvent SocketEvent;
        public SocketClientInfo ClientInfo;
        public Socket Socket;
        public byte[] Data { get; set; }
        public int Offset { get; set; }
        public int Count { get; set; }

        public SocketEventParam(Socket socket, EN_SocketEvent socketEvent)
        {
            SocketEvent = socketEvent;
            Socket = socket;
        }

        public SocketEventParam()
        {
        }
    }
}