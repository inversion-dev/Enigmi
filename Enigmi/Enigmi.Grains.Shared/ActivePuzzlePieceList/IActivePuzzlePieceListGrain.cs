using Enigmi.Common.Messaging;
using Enigmi.Domain.ValueObjects;
using Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;
using Enigmi.Grains.Shared.UserWallet.Messages;

namespace Enigmi.Grains.Shared.ActivePuzzlePieceList;

public interface IActivePuzzlePieceListGrain : IGrainWithIntegerKey
{
    Task<ResultOrError<UpdateActivePuzzlePiecesResponse>> UpdateActivePuzzlePieces(UpdateActivePuzzlePiecesCommand command);
    
    Task<GetStateResponse> GetActivePuzzlePieces(string stakingAddress);
    
    Task<GetStateResponse> GetActivePuzzlePieces(IEnumerable<string> puzzlePieceIds);

    Task ProcessSignalRMessageQueue();

    Task<GetPotentialTradeResponse> FindPotentialTrades(string initiatingStakingAddress, Guid puzzlePieceDefinitionId);

    Task<TradeDetail?> GetPotentialTrade(string initiatingStakingAddress, string initiatingPuzzlePieceId,
        string counterpartyPuzzlePieceId, string counterpartyPieceStakingAddress);

    Task<ResultOrError<GetPuzzleDefinitionsResponse>> GetPuzzleDefinitions(IEnumerable<Guid> puzzlePieceDefinitionIds);
}