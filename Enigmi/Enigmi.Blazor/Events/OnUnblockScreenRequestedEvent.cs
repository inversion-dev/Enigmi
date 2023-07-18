using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnUnblockScreenRequestedEvent
{
    public event EventHandler? OnUnblockScreenRequested;
    
    public void Trigger()
    {
        OnUnblockScreenRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public void Subscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnUnblockScreenRequested += handler;
    }
    
    public void UnSubscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnUnblockScreenRequested -= handler;
    }
}