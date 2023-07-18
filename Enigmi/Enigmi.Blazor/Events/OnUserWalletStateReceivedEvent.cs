using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnUserWalletStateReceivedEvent
{
    public event EventHandler<WalletStateReceivedEventArgs>? OnUserWalletStateReceived;
    
    public void Trigger(int ownedPuzzlePiecesCount)
    {
        ownedPuzzlePiecesCount.ThrowIf(x => x < 0);
        OnUserWalletStateReceived?.Invoke(this, new WalletStateReceivedEventArgs(ownedPuzzlePiecesCount));
    }
    
    public void Subscribe(EventHandler<WalletStateReceivedEventArgs> handler)
    {
        handler.ThrowIfNull();
        OnUserWalletStateReceived += handler;
    }
    
    public void UnSubscribe(EventHandler<WalletStateReceivedEventArgs> handler)
    {
        handler.ThrowIfNull();
        OnUserWalletStateReceived -= handler;
    }
}

public class WalletStateReceivedEventArgs : EventArgs
{
    public int OwnedPuzzlePiecesCount { get; set; }

    public WalletStateReceivedEventArgs(int ownedPuzzlePiecesCount)
    {
        OwnedPuzzlePiecesCount = ownedPuzzlePiecesCount;
    }
}