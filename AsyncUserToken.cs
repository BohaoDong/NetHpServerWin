using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace NetHpServer
{

    public class AsyncUserToken
    {
        public AsyncUserToken()
        {
            this.Buffer = new List<byte>();
        }

        /// <summary>
        /// 通信socket
        /// </summary>
        public Socket Socket { get; set; }
        /// <summary>  
        /// 客户端IP地址  
        /// </summary>  
        public IPAddress ClientIpAddress { get; set; }

        /// <summary>  
        /// 远程地址  
        /// </summary>  
        public EndPoint ClientRemote { get; set; }

        /// <summary>
        /// 连接的时间
        /// </summary>
        public DateTime ConnectTime { get; set; }

        /// <summary>  
        /// 所属用户信息  
        /// </summary>  
        public UserInfoModel UserInfo { get; set; }

        /// <summary>  
        /// 数据缓存区  
        /// </summary>  
        public List<byte> Buffer { get; set; }

    }
    public class UserInfoModel
    {
    }
}
