using System;
using System.Net.Sockets;

namespace NetHpServer.model
{
    public class SocketClientInfo
    {
        public bool IsServer { get; set; }
        public object Tag { get; internal set; }
        public string LocalIp { get; internal set; }
        public int LocalPort { get; internal set; }

        /// <summary>
        /// 对端IP
        /// </summary>
        public string PeerIp { get; internal set; }
        public string PeerRemoteEndPoint { get; internal set; }
        /// <summary>
        /// 对端Port
        /// </summary>
        public int PeerPort { get; internal set; }
        internal void SetClientInfo(Socket socket)
        {
            PeerRemoteEndPoint = IsServer ? socket.RemoteEndPoint.ToString() : socket.LocalEndPoint.ToString();
        }
    }
}