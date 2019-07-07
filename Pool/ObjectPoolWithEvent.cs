using System;
using System.Collections.Concurrent;
using System.Threading;
using NetHpServer.model;

namespace NetHpServer.Pool
{
    public class ObjectPool<T> : ObjectPoolWithEvent<T> where T : class
    {
    }

    public class ObjectPoolWithEvent<T> where T : class
    {
        protected ConcurrentQueue<T> ObjQueue = new ConcurrentQueue<T>();

        /// <summary>
        /// 信号机制
        /// </summary>
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);

        /// <summary>
        /// 信号机制，等待一个，如果没有会有一个超时时间的等待
        /// </summary>
        /// <param name="v">超时时间</param>
        internal virtual void WaitOne(int v)
        {
            _autoResetEvent.WaitOne(1000);
        }

        internal virtual T GetObj()
        {
            ObjQueue.TryDequeue(out T obj);
            return obj;

        }

        internal virtual void PutObj(T obj)  //SocketEventDeal
        {
            _autoResetEvent.Set();
            ObjQueue.Enqueue(obj);
        }
    }
}