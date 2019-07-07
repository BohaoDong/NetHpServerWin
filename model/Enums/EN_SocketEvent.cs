namespace NetHpServer.model.Enums
{
    /// <summary>
    /// Socket 的状态
    /// </summary>
    public enum EN_SocketEvent
    {
        connect,
        accept,
        close,
        read,
        send,
        packetLenError
    }
}