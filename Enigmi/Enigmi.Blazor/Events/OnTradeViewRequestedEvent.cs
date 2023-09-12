using Enigmi.Common;
using Enigmi.Messages.UserWallet;

namespace Enigmi.Blazor.Events;

public class OnTradeViewRequestedEvent
{
    public event EventHandler<RequestTradeViewEventArgs>? OnRequestTradeView;

    public void Trigger(GetActiveTradeListResponse.Trade trade)
    {
        OnRequestTradeView?.Invoke(this, new RequestTradeViewEventArgs(trade));
    }
    
    public void Subscribe(EventHandler<RequestTradeViewEventArgs> handler)
    {
        handler.ThrowIfNull();
        OnRequestTradeView += handler;
    }
    
    public void UnSubscribe(EventHandler<RequestTradeViewEventArgs> handler)
    {
        handler.ThrowIfNull();
        OnRequestTradeView -= handler;
    }
}

public class RequestTradeViewEventArgs : EventArgs
{
    public GetActiveTradeListResponse.Trade Trade { get; set; }

    public RequestTradeViewEventArgs(GetActiveTradeListResponse.Trade trade)
    {
        Trade = trade;
    }
}