using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;
using Enigmi.Grains.Shared.UserWallet.Messages;

namespace Enigmi.Grains.Shared.ActivePuzzlePieceList;

public interface IActivePuzzlePieceListGrain : IGrainWithIntegerKey
{
    Task<ResultOrError<UpdateActivePuzzlePiecesResponse>> UpdateActivePuzzlePieces(UpdateActivePuzzlePiecesCommand command);
    
    Task<GetStateResponse> GetActivePuzzlePieces(string stakingAddress);
    
    Task<GetStateResponse> GetActivePuzzlePieces(IEnumerable<string> puzzlePieceIds, string requestingStakingAddress);

    Task ProcessSignalRMessageQueue();

    Task<GetPotentialTradeRequest> FindPotentialTrades(string initiatingStakingAddress, Guid puzzlePieceDefinitionId);
}