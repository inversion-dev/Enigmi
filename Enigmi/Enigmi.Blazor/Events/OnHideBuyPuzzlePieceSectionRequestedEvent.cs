using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnHideBuyPuzzlePieceSectionRequestedEvent
{
    public event EventHandler? OnHideBuyPuzzlePieceSectionRequested;
    
    public void Trigger()
    {
        OnHideBuyPuzzlePieceSectionRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public void Subscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnHideBuyPuzzlePieceSectionRequested += handler;
    }

    public void UnSubscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnHideBuyPuzzlePieceSectionRequested -= handler;
    }
}