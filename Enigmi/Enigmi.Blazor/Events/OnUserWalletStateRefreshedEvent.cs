using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnUserWalletStateRefreshedEvent
{
    public event EventHandler? OnUserWalletStateRefreshed;
    
    public void Trigger()
    {
        OnUserWalletStateRefreshed?.Invoke(this, EventArgs.Empty);
    }
    
    public void Subscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnUserWalletStateRefreshed += handler;
    }
    
    public void UnSubscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnUserWalletStateRefreshed -= handler;
    }
}
