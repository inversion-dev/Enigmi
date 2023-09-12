using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PolicyAggregate.Events;
using Enigmi.Grains.Shared.Policy;
using Enigmi.Infrastructure.Services;
using Orleans.Providers;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;

namespace Enigmi.Application.Policy;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class PolicyGrain : GrainBase<Domain.Entities.PolicyAggregate.Policy>, IPolicyGrain
{
    private IPolicyVaultService PolicyVaultService { get; }

    public PolicyGrain(IPolicyVaultService policyVaultService)
    {
        PolicyVaultService = policyVaultService.ThrowIfNull();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
    }

    public override IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event)
    {
        @event.ThrowIfNull();

        var subscriptionNames = @event switch
        {
            PolicyCreated => Constants.PolicyCollectionGrainSubscription.ToSingletonList(),
            _ => string.Empty.ToSingletonList()
        };
        
        return subscriptionNames;
    }

    public async Task<ResultOrError<Constants.Unit>> CreatePolicy(string policyId, string mnemonicWords, uint policyClosingSlot, DateTime policyClosingUtcDate)
    {
        if (State.DomainAggregate != null)
        {
            throw new ApplicationException(Invariant($"Policy '{policyId}' has already been created"));
        }
        State.DomainAggregate = new Domain.Entities.PolicyAggregate.Policy(policyId.ThrowIfNullOrWhitespace(), policyClosingSlot, policyClosingUtcDate.ThrowIfNull());
        await PolicyVaultService.SetPolicyMnemonicAsync(policyId.ThrowIfNullOrWhitespace(), mnemonicWords.ThrowIfNullOrWhitespace());
        await WriteStateAsync();

        return new Constants.Unit().ToSuccessResponse();
    }

    public async Task<ResultOrError<string>> GetMnemonic()
    {
        State.DomainAggregate.ThrowIfNull();
        var mnemonic = await PolicyVaultService.GetPolicyMnemonicAsync(State.DomainAggregate.Id);
        return mnemonic.ToSuccessResponse();
    }

    public Task<Domain.Entities.PolicyAggregate.Policy> GetPolicy()
    {
        State.DomainAggregate.ThrowIfNull();
        return Task.FromResult(State.DomainAggregate);
    }
}