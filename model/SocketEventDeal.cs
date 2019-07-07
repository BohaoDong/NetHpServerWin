using NetHpServer.model.Enums;

namespace NetHpServer.model
{

    public class SocketEventDeal
    {
        public AsyncSocketClient Client { get; set; }
        public EN_SocketDealEvent SocketEvent { get; set; }
        public SocketEventDeal(AsyncSocketClient client, EN_SocketDealEvent socketEvent)
        {
            Client = client;
            SocketEvent = socketEvent;
        }
    }
}