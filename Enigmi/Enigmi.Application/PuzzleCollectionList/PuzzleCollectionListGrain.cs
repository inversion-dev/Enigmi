using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PuzzleCollectionAggregate.Events;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleCollectionList;
using Enigmi.Grains.Shared.PuzzleCollectionList.Messages;
using Orleans.Providers;
using Orleans.Runtime;

namespace Enigmi.Application.PuzzleCollectionList;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class PuzzleCollectionListGrain : GrainBase<Domain.Entities.PuzzleCollectionListAggregate.PuzzleCollectionList>, IPuzzleCollectionListGrain
{
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (State.DomainAggregate == null)
        {
            State.DomainAggregate = new Domain.Entities.PuzzleCollectionListAggregate.PuzzleCollectionList(this.GetGrainId().GetIntegerKey());
            await WriteStateAsync();
        }

        await Subscribe<PuzzleCollectionPublished>(Constants.PuzzleCollectionListGrainSubscription, OnPuzzleCollectionPublished);
    }

    private async Task OnPuzzleCollectionPublished(PuzzleCollectionPublished @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        var puzzleCollectionGrain = GrainFactory.GetGrain<IPuzzleCollectionGrain>(@event.PuzzleCollectionId);
        var puzzleCollection = await puzzleCollectionGrain.GetPuzzleCollection();
        puzzleCollection.ThrowIfNull();
        
        State.DomainAggregate.AddPuzzleCollection(new Domain.Entities.PuzzleCollectionListAggregate.ValueObjects.PuzzleCollection(
            puzzleCollection.Id,
            puzzleCollection.Title,
            string.Empty,
            puzzleCollection.PermittedPuzzleSize.ToList(),
            puzzleCollection.PuzzlePiecePriceInAda
            ));

        await WriteStateAsync();
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        @event.ThrowIfNull();
        return string.Empty;
    }
    
    public Task<ResultOrError<GetPuzzleCollectionsResponse>> GetPuzzleCollections(GetPuzzleCollectionsRequest request)
    {
        request.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var puzzleCollections = new List<GetPuzzleCollectionsResponse.PuzzleCollectionDto>();
        foreach (var puzzleCollection in State.DomainAggregate.PuzzleCollections)
        {
            var puzzleCollectionDto = new GetPuzzleCollectionsResponse.PuzzleCollectionDto(
                puzzleCollection.Id, 
                puzzleCollection.Title,
                puzzleCollection.PuzzlePiecePriceInAda,
                puzzleCollection.AvailableSizes
            );
            
            puzzleCollections.Add(puzzleCollectionDto);
        }

        var response = new GetPuzzleCollectionsResponse(puzzleCollections);
        return Task.FromResult(response.ToSuccessResponse());
    }
}