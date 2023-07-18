using Enigmi.Common;

namespace Enigmi.Blazor.Events;

public class OnShowScreenBlockerEvent
{
    public event EventHandler<BlockScreenEventArgs>? OnBlockScreenRequested;
    
    public void Trigger(string text)
    {
        OnBlockScreenRequested?.Invoke(this, new BlockScreenEventArgs(text.ThrowIfNullOrWhitespace()));
    }   
    
    public void Subscribe(EventHandler<BlockScreenEventArgs> handler)
    {
        handler.ThrowIfNull();
        OnBlockScreenRequested += handler;
    }
    
    public void UnSubscribe(EventHandler<BlockScreenEventArgs> handler)
    {
        handler.ThrowIfNull();
        OnBlockScreenRequested -= handler;
    }
}

public class BlockScreenEventArgs : EventArgs
{
    public string Text { get; set; }

    public BlockScreenEventArgs(string text)
    {
        Text = text;
    }
}