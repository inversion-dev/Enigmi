using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnShowBuyPuzzlePieceSectionRequestedEvent
{
    public event EventHandler? OnShowBuyPuzzlePieceSectionRequested;

    public void Trigger()
    {
        OnShowBuyPuzzlePieceSectionRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Subscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnShowBuyPuzzlePieceSectionRequested += handler;
    }
    
    public void UnSubscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnShowBuyPuzzlePieceSectionRequested -= handler;
    }
}