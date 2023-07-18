using Enigmi.Common.Messaging;

namespace Enigmi.Grains.Shared.GrainSettings;

public interface IGrainSettingsGrain : IGrainWithIntegerKey
{
    ValueTask<Domain.Entities.GrainSettings.Settings> GetSettings();
    
    Task<ResultOrError<Constants.Unit>> UpdateSettings(Domain.Entities.GrainSettings.Settings updatedSettings);
}