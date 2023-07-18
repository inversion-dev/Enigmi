using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PuzzleCollection.Messages;

namespace Enigmi.Grains.Shared.PuzzleCollection;

using Orleans.Concurrency;

public interface IPuzzleCollectionGrain : IGrainWithGuidKey
{
    [AlwaysInterleave]
    Task<Domain.Entities.PuzzleCollectionAggregate.PuzzleCollection?> GetPuzzleCollection();

    Task<ResultOrError<CreatePuzzleCollectionResponse>> CreatePuzzleCollection(CreatePuzzleCollectionCommand command);

    Task<ResultOrError<Constants.Unit>> ResetState();
}