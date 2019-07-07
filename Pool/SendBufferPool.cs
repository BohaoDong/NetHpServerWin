using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetHpServer.Pool
{
    public class SendBufferPool
    {
        /// <summary>
        /// 发送消息缓存池
        /// </summary>
        private readonly ConcurrentQueue<byte[]> _bufferPool = new ConcurrentQueue<byte[]>();

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
        /// <summary>
        /// 缓存池存数据总长度
        /// </summary>
        public long BufferByteCount;

        /// <summary>
        /// 添加一条发送消息
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool PutObj(byte[] obj)
        {
            try
            {
                _autoResetEvent.Set();
                _bufferPool.Enqueue(obj);
                lock (this)
                {
                    BufferByteCount += obj.Length;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"**_bufferPool Enqueue异常!{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取一条发送消息
        /// </summary>
        /// <returns></returns>
        public byte[] GetObj()
        {
            if (_bufferPool.TryDequeue(out var result))
            {
                lock (this)
                {
                    BufferByteCount -= result.Length;
                }
            }
            return result;
        }
    }
}
