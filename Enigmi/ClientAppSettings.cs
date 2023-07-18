
public class ClientAppSettings
{

    public ClientAppSettings(IConfiguration config)
    {
        ApiUrl = config[nameof(ApiUrl)];
    }


    public string ApiUrl { get; init; }
}