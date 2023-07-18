using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PolicyListAggregate;
using Enigmi.Domain.Entities.PolicyListAggregate.Events;
using Enigmi.Domain.Entities.PuzzleCollectionAggregate.Events;
using Enigmi.Domain.Entities.PuzzleDefinitionAggregate.Events;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleCollection.Messages;
using Enigmi.Grains.Shared.PuzzleDefinition;
using Enigmi.Grains.Shared.PuzzleDefinition.Messages;
using Enigmi.Infrastructure.Services.BlobStorage;
using Orleans.Providers;
using Orleans.Runtime;

namespace Enigmi.Application.PuzzleCollection;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class PuzzleCollectionGrain : GrainBase<Enigmi.Domain.Entities.PuzzleCollectionAggregate.PuzzleCollection>, IPuzzleCollectionGrain
{
    private IBlobStorageService BlobStorageService { get; }

    public PuzzleCollectionGrain(IBlobStorageService blobStorageService)
    {
        BlobStorageService = blobStorageService;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        await Subscribe<PolicyAdded>(this.GetGrainId().GetGuidKey().ToString(), OnPolicyAdded);
        await Subscribe<PuzzleDefinitionPublished>(this.GetGrainId().GetGuidKey().ToString(), OnPuzzleDefinitionPublished);
    }
    
    private Task OnPuzzleDefinitionPublished(PuzzleDefinitionPublished @event)
    {
        State.DomainAggregate.ThrowIfNull();

        State.DomainAggregate.MarkAsPublished(@event.PuzzleDefinitionId);

        MarkAsPublishedIfConditionsAreMet();
        
        return Task.CompletedTask;
    }

    private async Task OnPolicyAdded(PolicyAdded @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        if (@event.PolicyType == PolicyType.Puzzle)
        {
            State.DomainAggregate.SetPuzzlePolicy(State.DomainAggregate.PuzzlePolicy with { PolicyId = @event.PolicyId });
        }

        if (@event.PolicyType == PolicyType.PuzzlePiece)
        {
            State.DomainAggregate.SetPuzzlePiecePolicy(State.DomainAggregate.PuzzlePiecePolicy with { PolicyId = @event.PolicyId });            
        }

        MarkAsPublishedIfConditionsAreMet();

        await WriteStateAsync();
    }

    private void MarkAsPublishedIfConditionsAreMet()
    {
        State.DomainAggregate.ThrowIfNull();
        if (State.DomainAggregate.CanBeMarkedAsPublished)
        {
            State.DomainAggregate.MarkedAsPublished();
        }
    }

    public Task<Domain.Entities.PuzzleCollectionAggregate.PuzzleCollection?> GetPuzzleCollection()
    {
        return Task.FromResult(State.DomainAggregate);
    }

    public async Task<ResultOrError<Constants.Unit>> ResetState()
    {
        if (State.DomainAggregate != null)
        {
            foreach (var puzzleDefinition in State.DomainAggregate.PuzzleDefinitions)
            {
                var puzzleDefinitionGrain = GrainFactory.GetGrain<IPuzzleDefinitionGrain>(puzzleDefinition.Id);
                await puzzleDefinitionGrain.ResetState();
            }

            await this.ClearStateAsync();
        }

        return new Constants.Unit().ToSuccessResponse();
    }

    private async Task CreatePuzzleDefinition(string puzzleName)
    {
        State.DomainAggregate.ThrowIfNull();
        puzzleName.ThrowIfNull();

        var puzzleCollection = State.DomainAggregate;

        var command = new CreatePuzzleDefinitionCommand(
            puzzleCollection.Id,
            puzzleCollection.GetPuzzleDefinitionImageSourceBlobPath(puzzleName),
            puzzleCollection.GetPuzzleDefinitionConfigSourceBlobPath(puzzleName)
        );

        var puzzleDefinitionId = Guid.NewGuid();
        var puzzleDefinitionGrain = GrainFactory.GetGrain<IPuzzleDefinitionGrain>(puzzleDefinitionId);
        await puzzleDefinitionGrain.CreatePuzzleDefinition(command);

        var detail = await puzzleDefinitionGrain.GetPuzzleDefinition();
        puzzleCollection.AddPermittedPuzzleSize(detail!.PuzzleSize);
        puzzleCollection.AddPuzzleDefinition(puzzleDefinitionId);
    }

    public async Task<ResultOrError<CreatePuzzleCollectionResponse>> CreatePuzzleCollection(CreatePuzzleCollectionCommand command)
    {
        command.ThrowIfNull();

        if (State.DomainAggregate != null)
            throw new ApplicationException(Invariant($"{State.DomainAggregate.GetType().Name} with id '{this.GetGrainId().GetGuidKey()}' has already been created'."));

        Guid puzzleCollectionId = this.GetGrainId().GetGuidKey();

        var config = await BlobStorageService.DownloadBlob<PuzzleCollectionDto>(Invariant($"{command.SourceBlobFolderPath}/config.json"));

        var puzzleCollection = new Domain.Entities.PuzzleCollectionAggregate.PuzzleCollection(
            puzzleCollectionId,
            config.Title,
            config.Description,
            config.PuzzlePolicyClosingUtcDate,
            config.PuzzlePiecePolicyClosingUtcDate);

        puzzleCollection.SetSourceBlobFolderPath(command.SourceBlobFolderPath);
        State.DomainAggregate = puzzleCollection;

        if (config.PuzzlePiecePriceInAda > 0)
        {
            puzzleCollection.SetPuzzlePiecePriceInAda(config.PuzzlePiecePriceInAda);
        }

        if (config.PuzzlePieceTradeInValueInAda > 0)
        {
            puzzleCollection.SetPuzzlePieceTradeInValueInAda(config.PuzzlePieceTradeInValueInAda);
        }

        if (!string.IsNullOrEmpty(config.CoverImageBlobPath))
        {
            puzzleCollection.SetCoverImageBlobPath(config.CoverImageBlobPath);
        }

        ParallelOptions options = new ParallelOptions()
        {
            TaskScheduler = TaskScheduler.Current
        };

        await Parallel.ForEachAsync(config.PuzzleNames, options, async (puzzleName, _) =>
        {
            await CreatePuzzleDefinition(puzzleName);
        });

        await WriteStateAsync();

        return new CreatePuzzleCollectionResponse().ToSuccessResponse();
    }

    private class PuzzleCollectionDto
    {
        public string Title { get; set; } = null!;

        public string Description { get; set; } = null!;

        public string CoverImageBlobPath { get; set; } = null!;

        public List<string> PuzzleNames { get; set; } = null!;

        public decimal PuzzlePiecePriceInAda { get; set; }

        public decimal PuzzlePieceTradeInValueInAda { get; set; }

        public DateTime PuzzlePolicyClosingUtcDate { get; set; }

        public DateTime PuzzlePiecePolicyClosingUtcDate { get; set; }
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var subscriptionName = @event switch
        {
            PuzzleCollectionCreated => Constants.PolicyCollectionGrainSubscription,
            PuzzlePiecePolicyAdded => State.DomainAggregate.Id.ToString(),
            PuzzleCollectionPublished => Constants.PuzzleCollectionListGrainSubscription,
            _ => string.Empty
        };

        return subscriptionName;
    }
}