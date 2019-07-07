namespace NetHpServer.model.Enums
{
    /// <summary>
    /// 读取数据的状态
    /// </summary>
    public enum EN_SocketReadResult
    {
        /// <summary>
        /// 异步读取
        /// </summary>
        InAsyn,
        /// <summary>
        /// 同步读取
        /// </summary>
        HaveRead,
        /// <summary>
        /// 读取出错
        /// </summary>
        ReadError
    }
}