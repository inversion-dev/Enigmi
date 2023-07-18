using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.GrainSettings;

public class GrainSettings : DomainEntity
{
    public GrainSettings()
    {
        Settings = new();
    }
    
    [JsonProperty]
    public Settings Settings { get; private set; }

    public ResultOrError<Enigmi.Constants.Unit> UpdateSettings(Settings updatedSettings)
    {
        updatedSettings.ThrowIfNull();
        if (updatedSettings.Version != Settings.Version)
        {
            return "Version mismatched".ToFailedResponse<Enigmi.Constants.Unit>();
        }
        
        Settings = updatedSettings;
        Settings.Version++;

        return new Enigmi.Constants.Unit().ToSuccessResponse();
    }  
}