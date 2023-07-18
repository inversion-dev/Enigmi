using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PuzzlePiece.Messages;
using Orleans.Concurrency;

namespace Enigmi.Grains.Shared.PuzzlePiece;

public interface IPuzzlePieceGrain : IGrainWithStringKey
{
	[AlwaysInterleave]
	public Task<Domain.Entities.PuzzlePieceAggregate.PuzzlePiece?> GetPuzzlePiece();
	
    Task<ResultOrError<CreatePuzzlePieceResponse>> CreatePuzzlePiece(CreatePuzzlePieceCommand command);

    public Task<ResultOrError<Constants.Unit>> ResetState();
}