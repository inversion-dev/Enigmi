using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PolicyAggregate.Events;
using Enigmi.Domain.Entities.PolicyListAggregate;
using Enigmi.Domain.Entities.PolicyListAggregate.Events;
using Enigmi.Domain.Entities.PolicyListAggregate.ValueObjects;
using Enigmi.Domain.Entities.PuzzleCollectionAggregate.Events;
using Enigmi.Grains.Shared.Policy;
using Enigmi.Grains.Shared.PolicyCollection;
using Enigmi.Infrastructure.Services.BlockchainService;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace Enigmi.Application.PolicyList;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class PolicyListGrain : GrainBase<Domain.Entities.PolicyListAggregate.PolicyList>, IPolicyListGrain
{
    private ILogger<PolicyListGrain> Logger { get; }

    private IBlockchainService BlockchainService { get; }

    public PolicyListGrain(IBlockchainService blockchainService, ILogger<PolicyListGrain> logger)
    {
        Logger = logger;
        BlockchainService = blockchainService.ThrowIfNull();
    }
    
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (State.DomainAggregate == null)
        {
            State.DomainAggregate = new Domain.Entities.PolicyListAggregate.PolicyList(0);
            await WriteStateAsync();
        }

        await Subscribe<PolicyCreated>(Constants.PolicyCollectionGrainSubscription, OnPolicyCreated);
        await Subscribe<PuzzleCollectionCreated>(Constants.PolicyCollectionGrainSubscription, OnPuzzleCollectionCreated);
    }

    private Task OnPolicyCreated(PolicyCreated @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.MarkAsAdded(@event.PolicyId);

        return Task.CompletedTask;
    }

    private async Task OnPuzzleCollectionCreated(PuzzleCollectionCreated @event)
    {
        @event.ThrowIfNull();
        
        await CreatePolicy(@event.PuzzleCollectionId, @event.PuzzlePolicyClosingUtcDate, PolicyType.Puzzle);
        await CreatePolicy(@event.PuzzleCollectionId, @event.PuzzlePiecePolicyClosingUtcDate, PolicyType.PuzzlePiece);
    }

    private async Task<ResultOrError<Constants.Unit>> CreatePolicy(Guid puzzleCollectionId, DateTime policyClosingUtcDate, PolicyType policyType)
    {
        policyClosingUtcDate.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var slotAndFees = await this.BlockchainService.GetSlotAndFeesAsync();
        var generatePolicyResult = State.DomainAggregate.GeneratePolicy(policyClosingUtcDate, slotAndFees.Slot.Slot);

        var addPolicyResponse = State.DomainAggregate.AddPolicy(
            puzzleCollectionId.ThrowIfEmpty(), 
            generatePolicyResult.policyIdString, 
            policyType);
        
        if (addPolicyResponse.HasErrors)
        {
            return addPolicyResponse;
        }
        
        await WriteStateAsync();
        
        var policyGrain = GrainFactory.GetGrain<IPolicyGrain>(generatePolicyResult.policyIdString);
        var createPolicyResponse = await policyGrain.CreatePolicy(
            generatePolicyResult.policyIdString, 
            generatePolicyResult.mnemonic.Words,
            generatePolicyResult.policyClosingSlot,
            policyClosingUtcDate);

        if (createPolicyResponse.HasErrors)
        { 
            foreach (var error in createPolicyResponse.Errors)
            {
                Logger.LogError(error);    
            }
            return createPolicyResponse;
        }
            
        return new Constants.Unit().ToSuccessResponse();
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var subscriptionName = @event switch
        {
            PolicyAdded policyAddedEvent => State.DomainAggregate.Policies.Single(x => x.PolicyId == policyAddedEvent.PolicyId).PuzzleCollectionId.ToString(),
            _ => string.Empty
        };
        
        return subscriptionName;
    }

    public Task<ResultOrError<Constants.Unit>> Ping()
    {
        return Task.FromResult(new Constants.Unit().ToSuccessResponse());
    }

    public Task<IEnumerable<PolicyToPuzzleCollectionMap>> GetPolicies()
    {
        State.DomainAggregate.ThrowIfNull();
        return Task.FromResult(State.DomainAggregate.Policies);
    }
}