using Enigmi.Common;
using Microsoft.JSInterop;

namespace Enigmi.Blazor.Utils;

public class TabVisibilityHandler
{
    private IJSRuntime JsRuntime { get; }
    private DotNetObjectReference<TabVisibilityHandler> Reference { get; set; } = null!;
    public bool IsVisible { get; private set; } = true;

    public event EventHandler? OnVisibilityChanged;

    public TabVisibilityHandler(IJSRuntime jsRuntime)
    {
        JsRuntime = jsRuntime.ThrowIfNull();
    }

    public async Task Load()
    {
        Reference = DotNetObjectReference.Create(this);
        await JsRuntime.InvokeVoidAsync("registerJavascriptTabVisibilityHandlerReference", Reference);
    }

    [JSInvokable]
    public Task DocumentVisibilityChanged(bool visible)
    {
        if (IsVisible != visible)
        {
            Console.WriteLine($"visible: {visible}");
            IsVisible = visible;
            OnVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
        
        return Task.CompletedTask;
    }
}
