using System;
using System.Collections.Generic;

namespace NetHpServer.Pool
{
    internal class SocketEventPool<T>
    {
        protected static int num = 500;
        /// <summary>
        /// SocketEventPool的实例
        /// </summary>
        protected Stack<T> m_stack;
        public SocketEventPool()
        {
        }
        public SocketEventPool(int capacity)
        {
            m_stack = new Stack<T>(capacity);
        }

        protected void Push(T item)
        {
            if (item == null) { throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
            lock (m_stack)
            {
                m_stack.Push(item);
            }
        }

        /// <summary>
        /// 从池中移除 一个SocketAsyncEventArgs
        /// </summary>
        /// <returns>返回这个移除的SocketAsyncEventArgs</returns>
        protected T Pop()
        {
            lock (m_stack)
            {
                T result = m_stack.Pop();
                return result;
            }
        }

        /// <summary>
        /// 池中SocketAsyncEventArgs的数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (m_stack)
                {
                    return m_stack.Count;
                }
            }
        }

        /// <summary>
        /// 清空池中SocketAsyncEventArgs
        /// </summary>
        public void Clear()
        {
            lock (m_stack)
            {
                m_stack.Clear();
            }
        }
    }
}
