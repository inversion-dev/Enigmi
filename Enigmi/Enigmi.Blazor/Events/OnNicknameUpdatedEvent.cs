using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnNicknameUpdatedEvent
{
    public event EventHandler? OnNicknameUpdated;

    public void Trigger()
    {
        OnNicknameUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Subscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnNicknameUpdated += handler;
    }

    public void UnSubscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnNicknameUpdated -= handler;
    }
}
