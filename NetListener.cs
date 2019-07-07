using NetHpServer.Logger;
using NetHpServer.model;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetHpServer
{
    /// <summary>
    /// 监听端口，创建AsyncSocketClient。不对数据处理，只是创建监听到Client Socket
    /// </summary>
    public class NetListener
    {
        /// <summary>
        /// 监听Socket
        /// </summary>
        private Socket _listenSocket;

        /// <summary>
        /// 监听端口参数
        /// </summary>
        public ListenParam ListenParam { get; set; }

        /// <summary>
        /// 处理监听到的的AsyncSocketClient
        /// </summary>
        public event Action<ListenParam, AsyncSocketClient> OnAcceptSocket;

        /// <summary>
        /// 服务是否已启动
        /// </summary>
        private bool _start;

        /// <summary>
        /// Socket server
        /// </summary>
        private NetServer NetServer { get; }

        /// <summary>
        /// 连接Client的数量
        /// </summary>
        public int AcceptAsyncCount = 0;

        /// <summary>
        /// 客户端集合--以后设计成双向链表
        /// </summary>
        private readonly ConcurrentQueue<AsyncSocketClient> _newSocketClientQueue = new ConcurrentQueue<AsyncSocketClient>();

        /// <summary>
        /// 创建监听模块
        /// </summary>
        /// <param name="netServer"></param>
        public NetListener(NetServer netServer)
        {
            NetServer = netServer;
        }

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <returns></returns>
        public bool StartListen()
        {
            try
            {
                _start = true;
                IPEndPoint listenPoint = new IPEndPoint(IPAddress.Any, ListenParam.Port);
                _listenSocket = new Socket(listenPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _listenSocket.Bind(listenPoint);
                _listenSocket.Listen(200);

                Task.Run(() => NetProcess());  //维持多个监听Socket并行监听客户端请求

                StartAccept();
                return true;
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"**监听异常!{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 开始接收client的连接请求-没有循环处理，只处理第一个连接的请求
        /// </summary>
        /// <returns></returns>
        public bool StartAccept()
        {
            SocketAsyncEventArgs acceptEventArgs = new SocketAsyncEventArgs();
            acceptEventArgs.Completed += AcceptEventArg_Completed;

            bool willRaiseEvent = _listenSocket.AcceptAsync(acceptEventArgs);
            Interlocked.Increment(ref AcceptAsyncCount);
            if (!willRaiseEvent)
            {
                Interlocked.Decrement(ref AcceptAsyncCount);
                _acceptEvent.Set();
                acceptEventArgs.Completed -= AcceptEventArg_Completed;
                ProcessAccept(acceptEventArgs);
            }
            return true;
        }

        /// <summary>
        /// 客户端连接成功回调事件方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="acceptEventArgs"></param>
        private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs acceptEventArgs)
        {
            try
            {
                Interlocked.Decrement(ref AcceptAsyncCount);
                _acceptEvent.Set();
                acceptEventArgs.Completed -= AcceptEventArg_Completed;
                ProcessAccept(acceptEventArgs);
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"AcceptEventArg_Completed {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 根据连接请求建立AsyncSocketClient
        /// </summary>
        /// <param name="acceptEventArgs"></param>
        private void ProcessAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            try
            {
                using (acceptEventArgs)
                {
                    if (acceptEventArgs.AcceptSocket != null)
                    {
                        AsyncSocketClient client = new AsyncSocketClient(acceptEventArgs.AcceptSocket);
                        client.CreateClientInfo(this);

                        _newSocketClientQueue.Enqueue(client);
                        _acceptEvent.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"ProcessAccept {ex.Message}***", ex);
            }
        }

        /// <summary>
        /// 信号机制
        /// </summary>
        private readonly AutoResetEvent _acceptEvent = new AutoResetEvent(false);

        /// <summary>
        /// 这个线程接收以后的连接请求
        /// </summary>
        private void NetProcess()
        {
            while (_start)
            {
                DealNewAccept();
                _acceptEvent.WaitOne(1000 * 10);
            }
        }

        // 每秒可以起送应对几千个客户端连接。接收对方监听采用AcceptAsync，也是异步操作。有单独的线程负责处理Accept。
        // 线程会同时投递多个AcceptAsync，就是已经建立好多个socket，等待客户端连接。当客户端到达时，可以迅速生成可用socket。
        /// <summary>
        /// 处理新的连接
        /// </summary>
        private void DealNewAccept()
        {
            try
            {
                //维持10个并发监听listener
                if (AcceptAsyncCount <= 10)
                {
                    StartAccept();
                }

                //检查新的Client是不是有问题
                while (true)
                {
                    _newSocketClientQueue.TryDequeue(out AsyncSocketClient client);
                    if (client == null)
                        break;

                    DealNewAccept(client);
                }
            }
            catch (Exception ex)
            {
                NetLogger.Log($@"DealNewAccept 异常", ex);
            }
        }

        /// <summary>
        /// 检查client是不是有问题
        /// </summary>
        /// <param name="client"></param>
        private void DealNewAccept(AsyncSocketClient client)
        {
            client.SendBufferByteCount = NetServer.SendBufferBytePerClient;
            OnAcceptSocket?.Invoke(ListenParam, client);
        }
    }
}
