using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PuzzlePieceDispenser.Messages;

namespace Enigmi.Grains.Shared.PuzzlePieceDispenser;

public interface IPuzzlePieceDispenserGrain : IGrainWithStringKey
{
    Task<ResultOrError<ReserveRandomPuzzlePiecesResponse>> ReserveRandomPuzzlePieces(ReserveRandomPuzzlePiecesCommand command);

    Task<ResultOrError<Constants.Unit>> AddStock(IEnumerable<string> puzzlePieceIds);

    Task<ResultOrError<bool>> CommitReservation(Guid reservationId);

    Task<ResultOrError<Constants.Unit>> Release(Guid reservationId);

    Task ReleaseExpiredReservations();

    Task<bool> HasStockAvailable();

    Task<Domain.Entities.PuzzlePieceDispenserAggregate.PuzzlePieceDispenser> GetPuzzlePieceDispenser();

    Task UpdateDispenserExpiresTimespan(TimeSpan dispenserExpiresTimespan);
}