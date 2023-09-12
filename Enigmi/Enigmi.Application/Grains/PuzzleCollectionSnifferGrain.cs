using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PuzzleCollectionSnifferAggregate;
using Enigmi.Grains.Shared;
using Enigmi.Grains.Shared.PolicyCollection;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleCollection.Messages;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;
using Enigmi.Grains.Shared.PuzzleDefinition;
using Orleans.Providers;

namespace Enigmi.Application.Grains;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public sealed class PuzzleCollectionSnifferGrain : Grain<DomainGrainState<PuzzleCollectionSniffer>>, IPuzzleCollectionSnifferGrain
{
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        State.DomainAggregate ??= new PuzzleCollectionSniffer();

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task ResetPuzzleDefinition(string dropSourceBlobFolderPath)
    {
        State.DomainAggregate.ThrowIfNull();

        var puzzleCollectionId = State.DomainAggregate.RemovePuzzleCollection(dropSourceBlobFolderPath);

        if (puzzleCollectionId != null)
        {
            var puzzleCollectionGrain = GrainFactory.GetGrain<IPuzzleCollectionGrain>(puzzleCollectionId.Value);
            await puzzleCollectionGrain.ResetState();
        }
    }

    public async Task<ResultOrError<SeedPuzzleCollectionResponse>> SeedPuzzleCollection(SeedPuzzleCollectionCommand command)
    {
        command.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        await ResetPuzzleDefinition(command.DropSourceBlobFolderPath);

        var puzzleCollectionId = Guid.NewGuid();

        var puzzleCollectionGrain = GrainFactory.GetGrain<IPuzzleCollectionGrain>(puzzleCollectionId);
        
        var policyCollectionGrain = GrainFactory.GetGrain<IPolicyListGrain>(Constants.SingletonGrain);
        await policyCollectionGrain.Ping();

        var createPuzzleCollectionResponse = await puzzleCollectionGrain.CreatePuzzleCollection(new CreatePuzzleCollectionCommand(command.DropSourceBlobFolderPath));
        
        var response = createPuzzleCollectionResponse.Transform(o => new SeedPuzzleCollectionResponse(puzzleCollectionId));

        State.DomainAggregate.CreatePuzzleCollection(puzzleCollectionId, command.DropSourceBlobFolderPath);

        await WriteStateAsync();

        return response;
    }
}