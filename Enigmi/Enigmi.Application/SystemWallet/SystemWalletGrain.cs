using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Grains.Shared.SystemWallet;
using Orleans.Providers;

namespace Enigmi.Application.SystemWallet;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class SystemWalletGrain : GrainBase<Domain.Entities.SystemWalletAggregate.SystemWallet>, ISystemWalletGrain
{
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.DomainAggregate == null)
        {
            State.DomainAggregate = new Domain.Entities.SystemWalletAggregate.SystemWallet();
            await WriteStateAsync();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        return String.Empty;
    }

    public ValueTask<string> GetHumanFriendlyAddress()
    {
        State.DomainAggregate.ThrowIfNull();
        return ValueTask.FromResult(State.DomainAggregate.GetHumanFriendlyAddress());
    }
}