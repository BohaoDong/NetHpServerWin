using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetHpServer.Pool
{
    public class LinkedListEx<T> : LinkedList<T>
    {
        public static LinkedList<T> Instance = new LinkedList<T>();


        // 将一个链表中的节点放到头部
        private void MoveToHead(LinkedListNode<T> n)
        {
            Instance.Remove(n);
            Instance.AddFirst(n);
        }
    }
}
