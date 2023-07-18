using CardanoSharp.Blazor.Components.Extensions;

namespace Enigmi.Blazor;

public class ClientAppSettings
{
    public ClientAppSettings(IConfiguration config)
    {
        config.ThrowIfNull();
        
        ApiUrl = config[nameof(ApiUrl)]!;
    }
    public string ApiUrl { get; init; }
}