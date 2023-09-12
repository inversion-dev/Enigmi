using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared;
using Enigmi.Grains.Shared.GrainSettings;
using Orleans.Providers;

namespace Enigmi.Application.GrainSettings;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class GrainSettingsGrain : Grain<DomainGrainState<Domain.Entities.GrainSettings.GrainSettings>>, IGrainSettingsGrain
{
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        State.DomainAggregate ??= new Domain.Entities.GrainSettings.GrainSettings();
        return Task.CompletedTask;
    }

    public ValueTask<Domain.Entities.GrainSettings.Settings> GetSettings()
    {
        State.DomainAggregate.ThrowIfNull();
        return ValueTask.FromResult(State.DomainAggregate.Settings);
    }

    public async Task<ResultOrError<Constants.Unit>> UpdateSettings(Domain.Entities.GrainSettings.Settings updatedSettings)
    {
        State.DomainAggregate.ThrowIfNull();
        var updateSettingResponse = State.DomainAggregate.UpdateSettings(updatedSettings);

        if (updateSettingResponse.HasErrors)
        {
            return updateSettingResponse.Errors.ToFailedResponse<Constants.Unit>();
        }
        
        await WriteStateAsync();

        return new Constants.Unit().ToSuccessResponse();
    }
}