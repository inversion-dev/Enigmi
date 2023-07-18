using Enigmi.Blazor.Events;
using Enigmi.Blazor.Utils;
using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Components.ScreenBlocker;

public partial class ScreenBlocker
{
    [Inject] 
    private OnUnblockScreenRequestedEvent OnUnblockScreenRequestedEvent { get; set; } = null!;

    [Inject]
    private OnShowScreenBlockerEvent OnShowScreenBlockerEvent { get; set; } = null!;

    public string? Text { get; set; }

    public bool IsBlocking { get; set; }

    protected override void OnInitialized()
    {
        OnShowScreenBlockerEvent.Subscribe(OnBlockScreenRequested);
        OnUnblockScreenRequestedEvent.Subscribe(OnUnblockScreenRequested);
    }

    void OnBlockScreenRequested(object? sender, BlockScreenEventArgs e)
    {
        Text = e.Text;

        IsBlocking = true;
        StateHasChanged();
    }

    void OnUnblockScreenRequested(object? sender, EventArgs e)
    {
        Text = null;
        IsBlocking = false;
        StateHasChanged();
    }

    public void Dispose()
    {
        OnShowScreenBlockerEvent.UnSubscribe(OnBlockScreenRequested);
        OnUnblockScreenRequestedEvent.UnSubscribe(OnUnblockScreenRequested);
    }
}