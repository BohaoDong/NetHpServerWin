namespace NetHpServer.model
{
    /// <summary>
    /// 监听参数
    /// </summary>
    public class ListenParam
    {
        public int Port;
        public object Tag;

        public ListenParam(int port, object tag)
        {
            Port = port;
            Tag = tag;
        }
    }
}