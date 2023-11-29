using CardanoSharp.Blazor.Components.Extensions;

namespace Enigmi.Blazor;

public class ClientAppSettings
{
    public ClientAppSettings(IConfiguration config)
    {
        config.ThrowIfNull();
        
        ApiUrl = config[nameof(ApiUrl)]!;
        CardanoScanUrl = config[nameof(CardanoScanUrl)]!;
    }
    public string ApiUrl { get; init; }

    public string CardanoScanUrl { get; init; }
}