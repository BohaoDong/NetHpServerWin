using NetHpServer.model.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetHpServer.Logger;
using NetHpServer.model;
using NetHpServer.Pool;

namespace NetHpServer
{
    /// <summary>
    /// 客户端封装类
    /// </summary>
    public class AsyncSocketClient : IDisposable
    {
        #region 参数属性委托
        /// <summary>
        /// 每次读取接收数据的最大长度
        /// </summary>
        public static int IocpReadLen = 1024 * 10;

        /// <summary>
        /// connect socket
        /// </summary>
        public Socket ConnectSocket;

        //public long ClientName = CreateCount;

        protected SocketAsyncEventArgs m_receiveEventArgs;
        /// <summary>
        /// 连接的 SocketAsyncEventArgs
        /// </summary>
        public SocketAsyncEventArgs ReceiveEventArgs
        {
            get => m_receiveEventArgs;
            set => m_receiveEventArgs = value;
        }

        /// <summary>
        /// 接收缓存区
        /// </summary>
        protected byte[] m_asyncReceiveBuffer;

        /// <summary>
        /// 发送 SocketAsyncEventArgs
        /// </summary>
        protected SocketAsyncEventArgs m_sendEventArgs;

        /// <summary>
        /// 发送的 SocketAsyncEventArgs
        /// </summary>
        public SocketAsyncEventArgs SendEventArgs
        {
            get => m_sendEventArgs;
            set => m_sendEventArgs = value;
        }

        /// <summary>
        /// 发送缓冲区
        /// </summary>
        protected byte[] m_asyncSendBuffer;

        /// <summary>
        /// 读取消息事件--对外传递消息的接口
        /// </summary>
        public event Action<AsyncSocketClient, byte[], int, int> OnReadData;

        /// <summary>
        /// 发送消息事件
        /// </summary>
        public event Action<AsyncSocketClient, int> OnSendData;

        /// <summary>
        /// 关闭连接事件
        /// </summary>
        public event Action<AsyncSocketClient> OnSocketClose;

        /// <summary>
        /// 心跳事件
        /// </summary>
        public event Action<AsyncSocketClient> OnSocketHeart;

        private static readonly object ReleaseLock = new object();
        /// <summary>
        /// 记录当前 AsyncSocketClient 创建次数
        /// </summary>
        public static int CreateCount = 0;
        /// <summary>
        /// 记录当前 AsyncSocketClient 重置次数
        /// </summary>
        public static int ReleaseCount = 0;

        //析构函数
        ~AsyncSocketClient()
        {
            lock (ReleaseLock)
            {
                ReleaseCount++;
            }
        }
        #endregion

        #region init

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="socket">连接Socket</param>
        public AsyncSocketClient(Socket socket)
        {
            Init(socket);
        }

        public void Init(Socket socket)
        {
            DisposeRead = false;
            DisposeSend = false;
            SocketError = false;
            Interlocked.Increment(ref CreateCount);
            ConnectSocket = socket;

            m_asyncReceiveBuffer = new byte[IocpReadLen];
            m_receiveEventArgs = new SocketAsyncEventArgs();
            m_receiveEventArgs.AcceptSocket = ConnectSocket;
            m_receiveEventArgs.Completed += ReceiveEventArgs_Completed;

            m_asyncSendBuffer = new byte[IocpReadLen * 2];
            m_sendEventArgs = new SocketAsyncEventArgs();
            m_sendEventArgs.AcceptSocket = ConnectSocket;
            m_sendEventArgs.Completed += SendEventArgs_Completed;
        }

        /// <summary>
        /// 客户端信息
        /// </summary>
        public SocketClientInfo ClientInfo { get; private set; }

        /// <summary>
        /// 创建Client Info
        /// </summary>
        /// <param name="netListener"></param>
        internal void CreateClientInfo(NetListener netListener)
        {
            ClientInfo = new SocketClientInfo();
            try
            {
                ClientInfo.IsServer = true;
                ClientInfo.Tag = netListener.ListenParam.Tag;
                if (ConnectSocket != null)
                {
                    IPEndPoint ip = ConnectSocket.LocalEndPoint as IPEndPoint;
                    Debug.Assert(ip != null && netListener.ListenParam.Port == ip.Port);

                    ClientInfo.LocalIp = ip.Address.ToString();
                    ClientInfo.LocalPort = netListener.ListenParam.Port;

                    ip = ConnectSocket.RemoteEndPoint as IPEndPoint;
                    if (ip != null)
                    {
                        ClientInfo.PeerIp = ip.Address.ToString();
                        ClientInfo.PeerPort = ip.Port;
                        ClientInfo.PeerRemoteEndPoint = ConnectSocket.RemoteEndPoint.ToString();
                    }

                    //StartTimerTrigger();
                }
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"**创建ClientInfo异常!", ex);
            }
        }

        /// <summary>
        /// 直接设置外部Client Info
        /// </summary>
        /// <param name="clientInfo"></param>
        internal void SetClientInfo(SocketClientInfo clientInfo)
        {
            ClientInfo = clientInfo;
        }
        #endregion

        #region read process
        /// <summary>
        /// 消息没有读取完，继续读取
        /// </summary>
        private bool InReadPending { get; set; }

        /// <summary>
        /// 读取下一条数据
        /// </summary>
        /// <returns></returns>
        public EN_SocketReadResult ReadNextData()
        {
            lock (this)
            {
                if (SocketError)
                    return EN_SocketReadResult.ReadError;
                if (InReadPending)
                    return EN_SocketReadResult.InAsyn;
                if (!ConnectSocket.Connected)
                {
                    OnReadError();
                    return EN_SocketReadResult.ReadError;
                }
                try
                {
                    m_receiveEventArgs.SetBuffer(m_asyncReceiveBuffer, 0, m_asyncReceiveBuffer.Length);
                    InReadPending = true;
                    bool willRaiseEvent = ConnectSocket.ReceiveAsync(ReceiveEventArgs); //投递接收请求
                    if (!willRaiseEvent)
                    {
                        InReadPending = false;
                        //Console.WriteLine("同步接收");
                        ProcessReceive();
                        if (SocketError)
                        {
                            OnReadError();
                            return EN_SocketReadResult.ReadError;
                        }
                        return EN_SocketReadResult.HaveRead;
                    }
                    return EN_SocketReadResult.InAsyn;
                }
                catch (Exception ex)
                {
                    NetLogger.Log("ReadNextData", ex);
                    InReadPending = false;
                    OnReadError();
                    return EN_SocketReadResult.ReadError;
                }
            }
        }

        /// <summary>
        /// 异步读取完成事件方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            lock (this)
            {
                InReadPending = false;
                ProcessReceive();
                //if (SocketError)
                //{
                //    OnReadError();
                //}
            }
        }

        /// <summary>
        /// 处理接收消息方法
        /// </summary>
        private void ProcessReceive()
        {
            InReadPending = false;
            if (ReceiveEventArgs.SocketError == System.Net.Sockets.SocketError.Success && ReceiveEventArgs.BytesTransferred > 0)
            {
                int offset = ReceiveEventArgs.Offset;  //当前数据在收到的缓冲区的开始位置偏移量
                int count = ReceiveEventArgs.BytesTransferred;  //数据的长度

                //byte[] readData = new byte[count];
                //Array.Copy(m_asyncReceiveBuffer, offset, readData, 0, count);

                if (!SocketError)
                {
                    //StartTimerTrigger();
                    OnReadData?.Invoke(this, m_asyncReceiveBuffer, offset, count);
                }
            }
            else
            {
                //Console.WriteLine("接收错误");
                OnReadError();
            }
        }

        /// <summary>
        /// 标识Socket是否已经出问题-true:出现问题，false：没有问题
        /// </summary>
        private bool SocketError { get; set; }

        /// <summary>
        /// 读取数据出问题处理事件方法
        /// </summary>
        private void OnReadError()
        {
            lock (this)
            {
                if (!SocketError)
                {
                    SocketError = true;
                    OnSocketClose?.Invoke(this);
                }
                CloseClient();
            }
        }
        #endregion

        #region send process
        /// <summary>
        /// 发送数据最大长度
        /// </summary>
        private int _sendBufferByteCount = 1024 * 100;
        /// <summary>
        /// 发送消息的长度，有默认值，也可以自定义
        /// </summary>
        public int SendBufferByteCount
        {
            get => _sendBufferByteCount;
            set => _sendBufferByteCount = value < 1024 ? 1024 : value;
        }

        public bool InSendPending { get; private set; }

        /// <summary>
        /// 创建
        /// </summary>
        private readonly SendBufferPool _sendDataPool = new SendBufferPool();

        /// <summary>
        /// 消息放到发送缓存区中
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public EN_SendDataResult PutSendData(byte[] data)
        {
            lock (_sendDataPool)
            {
                if (ConnectSocket.Connected && _sendDataPool.PutObj(data))
                {
                    return EN_SendDataResult.ok;
                }
            }
            return EN_SendDataResult.no_client;
        }

        /// <summary>
        /// 发送下一条消息 
        /// </summary>
        /// <returns></returns>
        public EN_SocketSendResult SendNextData()
        {
            lock (this)
            {
                if (SocketError)
                    return EN_SocketSendResult.SendError;
                if (InSendPending)
                    return EN_SocketSendResult.InAsyn;
                if (!ConnectSocket.Connected)
                {
                    OnSendError();
                    return EN_SocketSendResult.SendError;
                }
                try
                {
                    var dataLen = GetSendData();
                    if (dataLen == 0)
                        return EN_SocketSendResult.NoSendData;
                    SendEventArgs.SetBuffer(m_asyncSendBuffer, 0, dataLen);
                    InSendPending = true;
                    bool willRaiseEvent = ConnectSocket.SendAsync(SendEventArgs);
                    if (!willRaiseEvent)
                    {
                        InSendPending = false;
                        ProcessSend(SendEventArgs);
                        if (SocketError)
                        {
                            OnSendError();
                            return EN_SocketSendResult.SendError;
                        }
                        return EN_SocketSendResult.HaveSend;
                    }
                    return EN_SocketSendResult.InAsyn;
                }
                catch (Exception ex)
                {
                    NetLogger.Log(string.Format("ReadNextData", ex));
                    InSendPending = false;
                    OnSendError();
                    return EN_SocketSendResult.SendError;
                }
            }
        }

        /// <summary>
        /// 发送消息完成事件方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="sendEventArgs"></param>
        private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs sendEventArgs)
        {
            lock (this)
            {
                try
                {
                    InSendPending = false;
                    SocketError = !ProcessSend(m_sendEventArgs);
                }
                catch (Exception ex)
                {
                    NetLogger.Log("SendEventArgs_Completed", ex);
                }
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="sendEventArgs"></param>
        /// <returns></returns>
        private bool ProcessSend(SocketAsyncEventArgs sendEventArgs)
        {
            if (sendEventArgs.SocketError == System.Net.Sockets.SocketError.Success)
            {
                var sendCount = sendEventArgs.BytesTransferred;
                OnSendData?.Invoke(this, sendCount);
                return true;
            }

            OnSendError();
            return false;
        }

        private byte[] _sendData = { };
        /// <summary>
        /// 获取一条发送消息
        /// </summary>
        /// <returns>消息的长度</returns>
        private int GetSendData()
        {
            int dataLen = 0;
            while (true)
            {
                _sendData = _sendDataPool.GetObj();
                if (_sendData == null)
                {
                    return dataLen;
                }
                //m_asyncSendBuffer = _sendData;
                Array.Copy(_sendData, 0, m_asyncSendBuffer, dataLen, _sendData.Length);
                dataLen = dataLen + _sendData.Length;
                //if (dataLen > IocpReadLen)  //指定取一定数据长度的数据
                break;
            }
            return dataLen;
        }

        /// <summary>
        /// 发送消息Error方法
        /// </summary>
        private void OnSendError()
        {
            lock (this)
            {
                if (!SocketError)
                {
                    SocketError = true;
                    OnSocketClose?.Invoke(this);
                }
                CloseClient();
            }
        }
        #endregion

        #region close
        internal void CloseSocket()
        {
            try
            {
                lock (ConnectSocket)
                {
                    ConnectSocket.Close();
                }
            }
            catch (Exception ex)
            {
                NetLogger.Log("CloseSocket", ex);
            }
        }

        private static readonly object SocketCloseLock = new object();

        /// <summary>
        /// 关闭SendSocket次数
        /// </summary>
        public static int CloseSendCount;

        /// <summary>
        /// 关闭ReadSocket次数
        /// </summary>
        public static int CloseReadCount;

        /// <summary>
        /// 是否已经释放了 m_sendEventArgs
        /// </summary>
        private bool DisposeSend { get; set; }

        /// <summary>
        /// 关闭 m_sendEventArgs
        /// </summary>
        private void CloseSend()
        {
            if (!DisposeSend && !InSendPending)
            {
                lock (SocketCloseLock)
                    CloseSendCount++;

                DisposeSend = true;
                m_sendEventArgs.SetBuffer(null, 0, 0);
                m_sendEventArgs.Completed -= SendEventArgs_Completed;
                m_sendEventArgs.Dispose();
            }
        }

        /// <summary>
        /// 是否释放了m_receiveEventArgs
        /// </summary>
        private bool DisposeRead { get; set; }

        /// <summary>
        /// 关闭 m_receiveEventArgs
        /// </summary>
        private void CloseRead()
        {
            if (!DisposeRead && !InReadPending)
            {
                lock (SocketCloseLock)
                    CloseReadCount++;

                DisposeRead = true;
                m_receiveEventArgs.SetBuffer(null, 0, 0);
                m_receiveEventArgs.Completed -= ReceiveEventArgs_Completed;
                m_receiveEventArgs.Dispose();
            }
        }

        /// <summary>
        /// 关闭Client
        /// </summary>
        private void CloseClient()
        {
            try
            {
                CloseRead();
                CloseSend();
                ConnectSocket.Close();
                Dispose();
                //AsyncSocketClientPool.Instance.PushSocketEventDeal(this);
            }
            catch (Exception ex)
            {
                NetLogger.Log("CloseClient", ex);
            }
        }
        #endregion

        /// <summary>
        /// 拆分长数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="maxLen"></param>
        /// <returns></returns>
        private List<byte[]> SplitData(byte[] data, int maxLen)
        {
            List<byte[]> items = new List<byte[]>();

            int start = 0;
            while (true)
            {
                int itemLen = Math.Min(maxLen, data.Length - start);
                if (itemLen == 0)
                    break;
                byte[] item = new byte[itemLen];
                Array.Copy(data, start, item, 0, itemLen);
                items.Add(item);
                start += itemLen;
            }
            return items;
        }

        #region  心跳机制

        private Timer _timer;
        private readonly TimeSpan _dueTime = TimeSpan.FromSeconds(Config.Config.Instance.HearIntervalTime); //TimeSpan.FromMinutes(1);  
        private readonly TimeSpan _periodTime = TimeSpan.FromSeconds(Config.Config.Instance.HearIntervalTime); //TimeSpan.FromMinutes(1);
        private bool _hasSendHeartBeat;

        private void StartTimerTrigger()
        {
            //if (_timer == null)
            //    _timer = new Timer(ExcuteJob, null, _dueTime, _periodTime);
            //else
            //{
            //    _timer.Change(_dueTime, _periodTime);
            //    _hasSendHeartBeat = false;
            //}
        }

        private void ExcuteJob(object obj)
        {
            try
            {
                //执行就表示Client在一段时间内没有发送消息过来--主动发送消息测试是否有收到消息
                if (!_hasSendHeartBeat)
                {
                    OnSocketHeart?.Invoke(this);
                    _hasSendHeartBeat = true;
                    _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                }
                else
                {
                    _timer.Dispose();
                    //NetLogger.Log($"{ConnectSocket?.RemoteEndPoint.ToString()} 没有心跳，主动断开！");
                    //CloseClient();
                }
            }
            catch (Exception e)
            {
                NetLogger.Log($"执行任务({nameof(GetType)})时出错，信息：{e}");
            }
        }

        public void Dispose()
        {
            ((IDisposable)ConnectSocket).Dispose();
        }
        #endregion
    }
}