using System;
using System.Net;
using System.Net.Sockets;
using NetHpServer.Logger;
using NetHpServer.model;
using NetHpServer.model.Enums;

namespace NetHpServer
{
    /// <summary>
    /// 最为客户端连接Manager
    /// </summary>
    public class NetConnectManage
    {
        /// <summary>
        /// 处理客户端的事件
        /// </summary>
        public event Action<SocketEventParam, AsyncSocketClient> OnSocketConnectEvent;

        /// <summary>
        /// 异步连接
        /// </summary>
        /// <param name="peerIp"></param>
        /// <param name="peerPort"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public bool ConnectAsyn(string peerIp, int peerPort, object tag)
        {
            try
            {
                Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                SocketAsyncEventArgs socketEventArgs = new SocketAsyncEventArgs();
                socketEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(peerIp), peerPort);
                socketEventArgs.Completed += SocketConnect_Completed;

                SocketClientInfo clientInfo = new SocketClientInfo();
                socketEventArgs.UserToken = clientInfo;
                clientInfo.PeerIp = peerIp;
                clientInfo.PeerPort = peerPort;
                clientInfo.Tag = tag;

                bool willRaiseEvent = socket.ConnectAsync(socketEventArgs);
                if (!willRaiseEvent)
                {
                    ProcessConnect(socketEventArgs);
                    socketEventArgs.Completed -= SocketConnect_Completed;
                    socketEventArgs.Dispose();
                }
                return true;
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"**ConnectAsyn**", ex);
                return false;
            }
        }

        /// <summary>
        /// 异步连接成功事件方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="socketEventArgs"></param>
        private void SocketConnect_Completed(object sender, SocketAsyncEventArgs socketEventArgs)
        {
            ProcessConnect(socketEventArgs);
            socketEventArgs.Completed -= SocketConnect_Completed;
            socketEventArgs.Dispose();
        }

        /// <summary>
        /// 处理Connect
        /// </summary>
        /// <param name="socketEventArgs"></param>
        private void ProcessConnect(SocketAsyncEventArgs socketEventArgs)
        {
            SocketClientInfo clientInfo = socketEventArgs.UserToken as SocketClientInfo;
            if (socketEventArgs.SocketError == SocketError.Success)
            {
                DealConnectSocket(socketEventArgs.ConnectSocket, clientInfo);
            }
            else
            {
                var socketParam = new SocketEventParam(null, EN_SocketEvent.connect)
                {
                    ClientInfo = clientInfo
                };
                OnSocketConnectEvent?.Invoke(socketParam, null);
            }
        }

        /// <summary>
        /// 处理连接的Socket
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="clientInfo"></param>
        public void DealConnectSocket(Socket socket, SocketClientInfo clientInfo)
        {
            clientInfo.SetClientInfo(socket);

            AsyncSocketClient client = new AsyncSocketClient(socket);
            client.SetClientInfo(clientInfo);

            //触发事件
            var socketParam = new SocketEventParam(socket, EN_SocketEvent.connect)
            {
                ClientInfo = clientInfo
            };
            OnSocketConnectEvent?.Invoke(socketParam, client);
        }

        /// <summary>
        /// 同步连接
        /// </summary>
        /// <param name="peerIp">IP</param>
        /// <param name="peerPort">端口</param>
        /// <param name="tag"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public bool Connect(string peerIp, int peerPort, object tag, out Socket socket)
        {
            socket = null;
            try
            {
                Socket socketTmp = new Socket(SocketType.Stream, ProtocolType.Tcp);

                SocketClientInfo clientInfo = new SocketClientInfo();
                clientInfo.PeerIp = peerIp;
                clientInfo.PeerPort = peerPort;
                clientInfo.Tag = tag;

                EndPoint remoteEP = new IPEndPoint(IPAddress.Parse(peerIp), peerPort);
                socketTmp.Connect(remoteEP);
                if (!socketTmp.Connected)
                    return false;

                DealConnectSocket(socketTmp, clientInfo);
                socket = socketTmp;
                return true;
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"连接对方:({peerIp}:{peerPort})出错！{ex.StackTrace}");
                return false;
            }
        }
    }
}
