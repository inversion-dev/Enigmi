using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class ActivePuzzlePieceUpdatedEvent
{
    public event EventHandler? OnActivePuzzlePieceUpdated;

    public void Trigger()
    {
        OnActivePuzzlePieceUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Subscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnActivePuzzlePieceUpdated += handler;
    }

    public void UnSubscribe(EventHandler handler)
    {
        handler.ThrowIfNull();
        OnActivePuzzlePieceUpdated -= handler;
    }
}