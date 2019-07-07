using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace NetHpServer.utility
{
    public class CheckPortEx
    {
        private static readonly int UdpPortStart = Config.Config.Instance.UdpPortStart;
        private static readonly int UdpPortEnd = Config.Config.Instance.UdpPortEnd;
        private static readonly object PortLock = new object();
        /// <summary>
        /// 从系统中获取两个不被占用的端口号
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<int> SetMediaPort()
        {
            lock (PortLock)
            {
                return Enumerable.Range(UdpPortStart, UdpPortEnd).Except(GetUsedUdpPorts.ToList()).Take(2);
            }
        }

        /// <summary>
        /// 检查端口号是否被专用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>true，被占用，false没被占用</returns>
        public static bool CheckUdpPort(int port)
        {
            return !GetUsedUdpPorts.Contains(port);
        }

        /// <summary>
        /// 检查端口号是否被专用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>true，被占用，false没被占用</returns>
        public static bool CheckTcpListenersPort(int port)
        {
            return GetTcpListeners.Contains(port);
        }


        /// <summary>
        /// 获取被占用的Udp端口
        /// </summary>
        private static IEnumerable<int> GetUsedUdpPorts =>
            IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
                .Where(p => p.Port >= UdpPortStart && p.Port < UdpPortEnd).OrderBy(a => a.Port)
                .Select(b => b.Port);

        /// <summary>
        /// 获取监听的Tcp端口
        /// </summary>
        private static IEnumerable<int> GetTcpListeners =>
            IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                .Where(p => p.Port >= UdpPortStart && p.Port < UdpPortEnd).OrderBy(a => a.Port)
                .Select(b => b.Port);

        /// <summary>
        /// 获取Tcp连接的信息
        /// </summary>
        private static IEnumerable<TcpConnectionInformation> GetTcpConnections =>
            IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
    }
}