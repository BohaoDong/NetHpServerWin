using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetHpServer.Logger;
using NetHpServer.model;
using NetHpServer.model.Enums;
using NetHpServer.Pool;
using NetHpServer.utility;

namespace NetHpServer
{
    public class SocketServer
    {
        public Action<SocketEventParam> OnSocketEvent { get; set; }

        public long SendByteCount
        {
            get
            {
                if (_netServer == null)
                    return 0;
                return _netServer.SendByteCount;
            }
        }

        public long ReadByteCount
        {
            get
            {
                if (_netServer == null)
                    return 0;
                return _netServer.ReadByteCount;
            }
        }

        private NetServer _netServer;

        private EN_PacketType _packetType = EN_PacketType.byteStream;

        /// <summary>
        /// 设定数据包的类型
        /// </summary>
        /// <param name="packetType"></param>
        public void SetPackageType(EN_PacketType packetType)
        {
            _packetType = packetType;
            _netServer?.SetPacketParam(packetType == EN_PacketType.byteStream ? 0 : 9, 1024);
        }

        /// <summary>
        /// 初始化--作为server开始监听
        /// </summary>
        /// <returns>true，开始监听，false，监听失败</returns>
        public bool Init()
        {
            var listenPort = Config.Config.Instance.TcpPortList ?? CheckPortEx.SetMediaPort().ToList();
            NetLogger.OnLogEvent += NetLogger_OnLogEvent;
            _netServer = new NetServer();
            SetPackageType(_packetType);

            _netServer.OnSocketPacketEvent += SocketPacketDeal;

            listenPort.ForEach(n =>
            {
                if (!CheckPortEx.CheckTcpListenersPort(n))
                    _netServer.AddListenPort(n, n);
                else
                    NetLogger_OnLogEvent($"端口号{n}已经被监听***");
            });

            var start = _netServer.StartListen(out List<int> listenFault);
            return start;
        }

        #region As Client connect server
        /// <summary>
        /// 异步连接服务器
        /// </summary>
        /// <param name="peerIp"></param>
        /// <param name="peerPort"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public bool ConnectAsync(string peerIp, int peerPort, object tag)
        {
            return _netServer.ConnectAsync(peerIp, peerPort, tag);
        }

        /// <summary>
        /// 同步连接服务器
        /// </summary>
        /// <param name="peerIp"></param>
        /// <param name="peerPort"></param>
        /// <param name="tag"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public bool Connect(string peerIp, int peerPort, object tag, out Socket socket)
        {
            return _netServer.Connect(peerIp, peerPort, tag, out socket);
        }
        #endregion

        /// <summary>
        /// 错误日志打印事件方法
        /// </summary>
        /// <param name="message"></param>
        private void NetLogger_OnLogEvent(string message)
        {
            Console.WriteLine(message);
        }

        #region 客户端集合处理
        public readonly Dictionary<Socket, SocketEventParam> _clientGroup = new Dictionary<Socket, SocketEventParam>();

        /// <summary>
        /// 当前Client数量
        /// </summary>
        public int ClientCount
        {
            get
            {
                lock (_clientGroup)
                {
                    return _clientGroup.Count;
                }
            }
        }

        public List<Socket> ClientList => _netServer != null ? _netServer.ClientList : new List<Socket>();

        private void AddClient(SocketEventParam socketParam)
        {
            lock (_clientGroup)
            {
                _clientGroup[socketParam.Socket] = socketParam;
            }
        }

        private void RemoveClient(SocketEventParam socketParam)
        {
            lock (_clientGroup)
            {
                var remove = _clientGroup.Remove(socketParam.Socket);
                if (!remove)
                {
                    Console.WriteLine($"移除{socketParam.ClientInfo.PeerRemoteEndPoint} 失败");
                }
            }
        }

        public Action<Socket, byte[]> OnSocketReceive { get; set; }

        /// <summary>
        /// 收到最终的字节流 事件方法
        /// </summary>
        /// <param name="socketParam"></param>
        private void SocketPacketDeal(SocketEventParam socketParam)
        {
            string peerIp = socketParam.ClientInfo.PeerRemoteEndPoint;
            switch (socketParam.SocketEvent)
            {
                case EN_SocketEvent.accept:
                    {
                        AddClient(socketParam);
                        NetLogger.Log($@"客户端链接!本地端口:{socketParam.ClientInfo.LocalPort},对端:{peerIp},客户端数量：{ClientCount}");
                        break;
                    }
                case EN_SocketEvent.close:
                    {
                        RemoveClient(socketParam);
                        NetLogger.Log($@"客户端断开!本地端口:{socketParam.ClientInfo.LocalPort},对端:{peerIp},剩余客户端数量：{ClientCount}");
                        break;
                    }
                case EN_SocketEvent.read:
                    {
                        var data = new byte[socketParam.Count];
                        Array.Copy(socketParam.Data, 0, data, 0, socketParam.Count);
                        OnSocketReceive?.Invoke(socketParam.Socket, data);
                        break;
                    }
                case EN_SocketEvent.connect:
                    {
                        if (socketParam.Socket != null)
                        {
                            AddClient(socketParam);
                            NetLogger.Log($@"连接对端成功!本地端口:{socketParam.ClientInfo.LocalPort},对端:{peerIp},客户端数量：{ClientCount}");
                        }
                        else
                        {
                            NetLogger.Log($@"连接对端失败!本地端口:{socketParam.ClientInfo.LocalPort},对端:{peerIp},客户端数量：{ClientCount}");
                        }

                        break;
                    }
            }

            //SocketEventParamPool.Instance.PushSocketEventDeal(socketParam);
        }

        /// <summary>
        /// 给某个客户端发送消息
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="data">数据</param>
        /// <returns></returns>
        public EN_SendDataResult SendData(Socket socket, byte[] data)
        {
            if (socket != null)
            {
                return _netServer.SendData(socket, data);
            }

            NetLogger.Log(@"还没连接！");
            return EN_SendDataResult.no_client;
        }

        public void SendDataEx(Socket socket, byte[] data)
        {
            if (socket != null)
            {
                _netServer.SendData(socket, data);
                return;
            }

            NetLogger.Log(@"还没连接！");
        }

        /// <summary>
        /// 给所有客户端发送消息
        /// </summary>
        /// <param name="data"></param>
        internal void SendToAll(byte[] data)
        {
            lock (_clientGroup)
            {
                Parallel.ForEach(_clientGroup.Keys, socket => SendData(socket, data));
            }
        }
        #endregion 
    }
}