using Enigmi.Common.Messaging;

namespace Enigmi.Messages.ActivePuzzlePieceList;

public record GetPotentialTradesRequest(string StakingAddress, Guid PuzzlePieceDefinitionId) : Request<GetPotentialTradesResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetPotentialTradesResponse(List<GetPotentialTradesResponse.TradeDetail> TradeDetails) : RequestResponse
{
    public record TradeDetail(TradePuzzlePiece OwnedPuzzlePiece, TradePuzzlePiece TradePuzzlePiece, decimal Rating);
    public record TradePuzzlePiece(string PuzzlePieceId, Guid PuzzleDefinitionId, string PuzzleDefinitionTitle, Guid PuzzleCollectionId, string PuzzleCollectionTitle, string StakingAddress, decimal Rating);   
}