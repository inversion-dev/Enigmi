using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnRequestPotentialTradeListEvent
{
    public event EventHandler<Guid>? OnRequestPotentialTradeListRequested;

    public void Trigger(Guid puzzlePieceDefinitionId)
    {
        OnRequestPotentialTradeListRequested?.Invoke(this, puzzlePieceDefinitionId);
    }

    public void Subscribe(EventHandler<Guid> handler)
    {
        handler.ThrowIfNull();
        OnRequestPotentialTradeListRequested += handler;
    }

    public void UnSubscribe(EventHandler<Guid> handler)
    {
        handler.ThrowIfNull();
        OnRequestPotentialTradeListRequested -= handler;
    }
}
