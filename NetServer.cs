using NetHpServer.Logger;
using NetHpServer.model;
using NetHpServer.model.Enums;
using NetHpServer.Pool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetHpServer
{
    public class NetServer
    {
        #region 参数 委托 事件
        public Action<SocketEventParam> OnSocketPacketEvent { get; set; }

        /// <summary>
        /// 每个连接发送缓冲大小
        /// </summary>
        public int SendBufferBytePerClient { get; set; } = 1024 * 100;

        /// <summary>
        /// 服务是否启动
        /// </summary>
        private bool ServerStart { get; set; }

        /// <summary>
        /// 负责对收到的字节流 组成完成的包
        /// </summary>
        private ClientPacketManage _clientPacketManage;

        /// <summary>
        /// 发送的字节总数
        /// </summary>
        public long SendByteCount { get; set; }
        /// <summary>
        /// 读取的字节总数
        /// </summary>
        public long ReadByteCount { get; set; }

        public int PacketMinLen { get; private set; }

        public int PacketMaxLen { get; private set; }

        /// <summary>
        /// 端口号集合
        /// </summary>
        private readonly List<ListenParam> _listListenPort = new List<ListenParam>();

        /// <summary>
        /// Listener集合--每个端口都对应一个
        /// </summary>
        private readonly List<NetListener> _listenerList = new List<NetListener>();

        public delegate int DelegateGetPacketTotalLen(byte[] data, int offset);

        /// <summary>
        /// 获取包的总长度 委托实例
        /// </summary> 
        /// <returns></returns>
        public DelegateGetPacketTotalLen GetPacketTotalLenCallback;
        #endregion

        #region 监听
        /// <summary>
        /// 添加监听的端口--监听之前要先运行这个方法添加端口
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="tag">tag标识</param>
        public void AddListenPort(int port, object tag)
        {
            _listListenPort.Add(new ListenParam(port, tag));
        }

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="listenFault">监听失败的端口集合</param>
        /// <returns>true，存在监听失败的端口，false，反之</returns>
        public bool StartListen(out List<int> listenFault)
        {
            ServerStart = true;

            _clientPacketManage = new ClientPacketManage(this);
            _clientPacketManage.OnSocketPacketEvent += PutClientPacket;
            _netConnectManage.OnSocketConnectEvent += SocketConnectEvent;

            _listenerList.Clear();
            Task.Run(() => NetReadProcess());
            Task.Run(() => NetSendProcess());
            Task.Run(() => NetPacketProcess());

            listenFault = new List<int>();
            foreach (ListenParam param in _listListenPort)
            {
                NetListener listener = new NetListener(this);
                listener.ListenParam = param;
                listener.OnAcceptSocket += Listener_OnAcceptSocketClient;  //返回Listener生成的 AsyncSocketClient
                if (!listener.StartListen())
                {
                    listenFault.Add(param.Port);
                }
                else
                {
                    _listenerList.Add(listener);
                    NetLogger.Log($@"监听成功！端口:{param.Port}");
                }
            }

            return listenFault.Count == 0;
        }

        /// <summary>
        /// Listener 生成的 AsyncSocketClient 回调方法
        /// </summary>
        /// <param name="listenParam">客户端参数</param>
        /// <param name="client">生成的客户端 实体类</param>
        private void Listener_OnAcceptSocketClient(ListenParam listenParam, AsyncSocketClient client)
        {
            try
            {
                lock (_clientGroup)
                {
                    _clientGroup[client.ConnectSocket] = client;
                }

                //给Client添加消息处理事件
                client.OnSocketClose += Client_OnSocketClose;
                client.OnReadData += Client_OnReadData;
                client.OnSendData += Client_OnSendData;
                client.OnSocketHeart += Client_OnSocketHeart;
                _listReadEvent.PutObj(new SocketEventDeal(client, EN_SocketDealEvent.read));
                var param = new SocketEventParam(client.ConnectSocket, EN_SocketEvent.accept)
                {
                    ClientInfo = client.ClientInfo
                };
                _socketEventPool.PutObj(param);
            }
            catch (Exception ex)
            {
                NetLogger.Log(@"Listener_OnAcceptSocket 异常", ex);
            }
        }

        private void Client_OnSocketHeart(AsyncSocketClient obj)
        {
            SendData(obj.ConnectSocket, System.Text.Encoding.Default.GetBytes("Heart beat test, please reply!"));
        }

        #endregion

        #region Clinet socket Collection
        /// <summary>
        /// 存储这所有的Socket与对应的AsyncSocketClient
        /// </summary>
        private readonly ConcurrentDictionary<Socket, AsyncSocketClient> _clientGroup = new ConcurrentDictionary<Socket, AsyncSocketClient>();

        /// <summary>
        /// 客户端数量
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

        /// <summary>
        /// 客户端Socket的List集合
        /// </summary>
        public List<Socket> ClientList
        {
            get
            {
                lock (_clientGroup)
                {
                    return _clientGroup.Keys.ToList();
                }
            }
        }
        #endregion

        #region  Read Process
        /// <summary>
        /// 存储着带有读操作的Client集合
        /// </summary>
        private readonly ObjectPoolWithEvent<SocketEventDeal> _listReadEvent = new ObjectPoolWithEvent<SocketEventDeal>();

        /// <summary>
        /// 新线程读取数据
        /// </summary>
        private void NetReadProcess()
        {
            while (true)
            {
                DealReadEvent();
                _listReadEvent.WaitOne(1000);
            }
        }

        /// <summary>
        /// 读取消息事件方法(仅仅是读这个操作，不涉及结果)
        /// </summary>
        private void DealReadEvent()
        {
            while (true)
            {
                SocketEventDeal item = _listReadEvent.GetObj();
                if (item == null)
                    break;
                switch (item.SocketEvent)
                {
                    case EN_SocketDealEvent.read:
                        {
                            while (true)
                            {
                                EN_SocketReadResult result = item.Client.ReadNextData();
                                if (result == EN_SocketReadResult.HaveRead)
                                    continue;
                                break;
                            }
                        }
                        break;
                    case EN_SocketDealEvent.send:
                        {
                            Debug.Assert(false);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 对读取到的数据进行处理--Client异步读取完后会执行这个事件方法
        /// </summary>
        /// <param name="client">当前客户端类</param>
        /// <param name="readData">读取到的数据</param>
        private void Client_OnReadData(AsyncSocketClient client, byte[] readData, int offset, int count)
        {
            _listReadEvent.PutObj(new SocketEventDeal(client, EN_SocketDealEvent.read)); //读下一条 
            try
            {
                var param = new SocketEventParam(client.ConnectSocket, EN_SocketEvent.read)
                {
                    ClientInfo = client.ClientInfo,
                    Data = readData,
                    Offset = offset,
                    Count = count
                };

                if (client.ConnectSocket.Connected)
                {
                    _socketEventPool.PutObj(param);

                    lock (this)
                    {
                        ReadByteCount += readData.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"Client_OnReadData 异常 {ex.Message}***{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 收到包长度异常的时间方法
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="packetLen"></param>
        internal void OnRcvPacketLenError(Socket socket, byte[] buffer, int offset, int packetLen)
        {
            try
            {
                lock (_clientGroup)
                {
                    if (!_clientGroup.Keys.Any(a => a == socket))
                    {
                        Debug.Assert(false);
                        return;
                    }

                    AsyncSocketClient client = _clientGroup[socket];
                    client.CloseSocket();
                }
            }
            catch (Exception ex)
            {
                NetLogger.Log("OnRcvPacketLenError 异常", ex);
            }
        }
        #endregion

        #region Send process
        /// <summary>
        /// 存储着带有写操作的Client集合
        /// </summary>
        private readonly ObjectPoolWithEvent<SocketEventDeal> _listSendEvent = new ObjectPoolWithEvent<SocketEventDeal>();

        /// <summary>
        /// 发送消息
        /// </summary>
        private void NetSendProcess()
        {
            while (true)
            {
                try
                {
                    DealSendEvent();
                    _listSendEvent.WaitOne(1000);
                }
                catch (Exception ex)
                {
                    NetLogger.Log($@"Error", ex);
                }
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        private void DealSendEvent()
        {
            while (true)
            {
                SocketEventDeal item = _listSendEvent.GetObj();
                if (item == null)
                    break;
                switch (item.SocketEvent)
                {
                    case EN_SocketDealEvent.send:
                        //while (true)
                        {
                            EN_SocketSendResult result = item.Client.SendNextData();
                            if (result == EN_SocketSendResult.InAsyn)
                                continue;
                            else
                                break;
                        }
                        break;
                    case EN_SocketDealEvent.read:
                        Debug.Assert(false);
                        break;
                }
            }
        }
        private void Client_OnSendData(AsyncSocketClient client, int sendCount)
        {
            //发送下一条 
            _listSendEvent.PutObj(new SocketEventDeal(client, EN_SocketDealEvent.send));
            lock (this)
            {
                SendByteCount += sendCount;
            }
        }

        /// <summary>
        /// 设置某个连接的发送缓冲大小
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="byteCount"></param>
        /// <returns></returns>
        public bool SetClientSendBuffer(Socket socket, int byteCount)
        {
            if (_clientGroup.Keys.Any(a => a == socket))
            {
                var client = _clientGroup[socket];
                client.SendBufferByteCount = byteCount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 放到发送缓冲
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public EN_SendDataResult SendData(Socket socket, byte[] data)
        {
            if (socket != null && _clientGroup.Keys.Any(a => a == socket))
            {
                AsyncSocketClient client = null;
                lock (_clientGroup)
                {
                    client = _clientGroup[socket];
                }

                EN_SendDataResult result = client.PutSendData(data);
                if (result == EN_SendDataResult.ok)
                {
                    //发送下一条  
                    _listSendEvent.PutObj(new SocketEventDeal(client, EN_SocketDealEvent.send));
                }

                return result;
            }

            return EN_SendDataResult.no_client;
        }
        #endregion

        #region Package process
        /// <summary>
        /// package中添加的事件方法
        /// </summary>
        /// <param name="param"></param>
        public void PutClientPacket(SocketEventParam param)
        {
            OnSocketPacketEvent?.Invoke(param);
        }

        /// <summary>
        /// 设置包的最小和最大长度
        /// 当minLen=0时，认为是接收字节流
        /// </summary>
        /// <param name="minLen"></param>
        /// <param name="maxLen"></param>
        public void SetPacketParam(int minLen, int maxLen)
        {
            Debug.Assert(minLen >= 0);
            Debug.Assert(maxLen > minLen);
            PacketMinLen = minLen;
            PacketMaxLen = maxLen;
        }

        /// <summary>
        /// 带有客户端信息及其收到的数据的集合
        /// </summary>
        private readonly ObjectPoolWithEvent<SocketEventParam> _socketEventPool = new ObjectPoolWithEvent<SocketEventParam>();

        /// <summary>
        /// 对收到的数据进行处理
        /// </summary>
        private void NetPacketProcess()
        {
            while (ServerStart)
            {
                try
                {
                    _socketEventPool.WaitOne(1000);
                    DealEventPool();
                }
                catch (Exception ex)
                {
                    NetLogger.Log($@"DealEventPool 异常 {ex.Message}***{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// 对数据的打包处理
        /// </summary>
        private void DealEventPool()
        {
            while (true)
            {
                SocketEventParam param = _socketEventPool.GetObj();
                if (param == null)
                    break;

                if (param.SocketEvent == EN_SocketEvent.close)
                {
                    var aa = _clientGroup.TryRemove(param.Socket, out _);
                    if (!aa)
                    {
                        Console.WriteLine("_clientGroup TryRemove False");
                    }
                }

                //if (PacketMinLen == 0) //字节流处理
                //{
                OnSocketPacketEvent?.Invoke(param);
                //}
            }
        }
        #endregion

        #region Connect process
        /// <summary>
        /// 客户端连接Manager
        /// </summary>
        private readonly NetConnectManage _netConnectManage = new NetConnectManage();

        /// <summary>
        /// 异步连接一个客户端
        /// </summary>
        /// <param name="peerIp"></param>
        /// <param name="peerPort"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public bool ConnectAsync(string peerIp, int peerPort, object tag)
        {
            return _netConnectManage.ConnectAsyn(peerIp, peerPort, tag);
        }

        /// <summary>
        /// 同步连接一个客户端
        /// </summary>
        /// <param name="peerIp"></param>
        /// <param name="peerPort"></param>
        /// <param name="tag"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public bool Connect(string peerIp, int peerPort, object tag, out Socket socket)
        {
            return _netConnectManage.Connect(peerIp, peerPort, tag, out socket);
        }

        /// <summary>
        /// 作为客户端连接后要对AsyncSocketClient的处理--事件方法
        /// </summary>
        /// <param name="param"></param>
        /// <param name="client"></param>
        private void SocketConnectEvent(SocketEventParam param, AsyncSocketClient client)
        {
            try
            {
                if (param.Socket == null || client == null) //连接失败
                {

                }
                else
                {
                    lock (_clientGroup)
                    {
                        _clientGroup[client.ConnectSocket] = client;
                    }

                    client.OnSocketClose += Client_OnSocketClose;
                    client.OnReadData += Client_OnReadData;
                    client.OnSendData += Client_OnSendData;

                    _listReadEvent.PutObj(new SocketEventDeal(client, EN_SocketDealEvent.read));
                }
                _socketEventPool.PutObj(param);
            }
            catch (Exception ex)
            {
                NetLogger.Log($"SocketConnectEvent 异常 {ex.Message}***", ex);
            }
        }
        #endregion

        private void Client_OnSocketClose(AsyncSocketClient client)
        {
            try
            {
                SocketEventParam param = new SocketEventParam(client.ConnectSocket, EN_SocketEvent.close);
                param.ClientInfo = client.ClientInfo;
                //Console.WriteLine("放入关闭命令");
                _socketEventPool.PutObj(param);
            }
            catch (Exception ex)
            {
                NetLogger.Log($"Client_OnSocketClose 异常 {ex.Message}***", ex);
            }
        }
    }
}