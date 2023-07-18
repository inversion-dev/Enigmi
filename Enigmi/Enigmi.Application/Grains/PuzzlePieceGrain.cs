using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PuzzlePieceAggregate;
using Enigmi.Grains.Shared;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.PuzzlePiece.Messages;
using Orleans.Providers;
using Orleans.Runtime;

namespace Enigmi.Application.Grains;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class PuzzlePieceGrain : Grain<DomainGrainState<PuzzlePiece>>, IPuzzlePieceGrain
{
    public Task<PuzzlePiece?> GetPuzzlePiece()
    {
        return Task.FromResult(State.DomainAggregate);
    }

    public async Task<ResultOrError<CreatePuzzlePieceResponse>> CreatePuzzlePiece(CreatePuzzlePieceCommand command)
    {
        command.ThrowIfNull();

        if (State.DomainAggregate != null)
            throw new ApplicationException(Invariant($"{State.DomainAggregate.GetType().Name} with id '{this.GetGrainId().GetGuidKey()}' has already been created'."));

        var puzzlePiece = new PuzzlePiece(this.GetPrimaryKeyString(), command.BlockchainAssetId, command.PuzzlePieceDefinitionId, command.PuzzleDefinitionId);
        State.DomainAggregate = puzzlePiece;

        await WriteStateAsync();

        return new CreatePuzzlePieceResponse().ToSuccessResponse();
    }

    public async Task<ResultOrError<Constants.Unit>> ResetState()
    {
        if (State.DomainAggregate != null)
        {
            await ClearStateAsync();
        }
        return new Constants.Unit().ToSuccessResponse();
    }
}
