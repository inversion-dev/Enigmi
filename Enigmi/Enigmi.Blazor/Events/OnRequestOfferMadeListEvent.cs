using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnRequestOfferMadeListEvent
{
    public event EventHandler? OnRequestOfferMadeListEventRequested;

    public void Trigger()
    {
        OnRequestOfferMadeListEventRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Subscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnRequestOfferMadeListEventRequested += handler;
    }

    public void UnSubscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnRequestOfferMadeListEventRequested -= handler;
    }
}