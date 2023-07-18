using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PuzzleDefinition.Messages;
using Orleans.Concurrency;

namespace Enigmi.Grains.Shared.PuzzleDefinition;

using Domain.Entities.PuzzleDefinitionAggregate;

public interface IPuzzleDefinitionGrain : IGrainWithGuidKey
{
    Task<ResultOrError<PingPuzzleDefinitionResponse>> Ping(PingPuzzleDefinitionCommand command);

    [AlwaysInterleave]
    Task<PuzzleDefinition?> GetPuzzleDefinition();

    Task<ResultOrError<CreatePuzzleDefinitionResponse>> CreatePuzzleDefinition(CreatePuzzleDefinitionCommand command);

    Task<ResultOrError<Constants.Unit>> ResetState();

    Task<ResultOrError<Constants.Unit>> UnsubscribeAll();
}