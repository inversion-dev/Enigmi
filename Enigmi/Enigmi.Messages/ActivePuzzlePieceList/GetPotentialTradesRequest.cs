using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;
using Enigmi.Domain.ValueObjects;

namespace Enigmi.Messages.ActivePuzzlePieceList;

public record GetPotentialTradesRequest(string StakingAddress, Guid PuzzlePieceDefinitionId) : Request<GetPotentialTradesResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetPotentialTradesResponse(List<GetPotentialTradesResponse.UserWalletTradeDetailList> TradeDetails) : RequestResponse
{
    public record UserWalletTradeDetailList(string StakingAddress, List<TradeDetail> TradeDetails);

    public record TradeDetail(TradeParty InitiatingParty, TradeParty Counterparty, decimal Rating);

    public record TradeParty(string PuzzlePieceId, Guid PuzzlePieceDefinitionId, Guid PuzzleDefinitionId,
        string PuzzleDefinitionTitle, Guid PuzzleCollectionId, string PuzzleCollectionTitle,
        string StakingAddress, string Nickname, decimal Rating,
        PuzzleDefinitionInventory OutgoingPuzzleDefinitionPieceDefinitionInventory,
        PuzzleDefinitionInventory IncomingPuzzleDefinitionPieceDefinitionInventory)
    {
        public static TradeParty ConvertToParty(TradePuzzlePiece piece)
        {
            return new TradeParty(
                piece.PuzzlePieceId,
                piece.PuzzlePieceDefinitionId,
                piece.PuzzleDefinitionId,
                piece.PuzzleDefinitionTitle,
                piece.PuzzleCollectionId,
                piece.PuzzleCollectionTitle,
                piece.StakingAddress,
                piece.Nickname,
                piece.Rating,
                new GetPotentialTradesResponse.PuzzleDefinitionInventory
                {
                    PuzzlePieceDefinitionIds = piece
                        .OutgoingPuzzleDefinitionPieceDefinitionInventory
                        .Select(z =>
                            new GetPotentialTradesResponse.PuzzleDefinitionInventoryItem(z.PuzzlePieceDefinitionId,
                                z.Quantity)).ToList()
                },
                new GetPotentialTradesResponse.PuzzleDefinitionInventory
                {
                    PuzzlePieceDefinitionIds = piece
                        .IncomingPuzzleDefinitionPieceDefinitionInventory
                        .Select(z =>
                            new GetPotentialTradesResponse.PuzzleDefinitionInventoryItem(z.PuzzlePieceDefinitionId,
                                z.Quantity)).ToList()
                }
            );
        }
    }
    
    public record PuzzleDefinitionInventory()
    {
        public List<PuzzleDefinitionInventoryItem> PuzzlePieceDefinitionIds { get; set; } = new ();
    }

    public record PuzzleDefinitionInventoryItem(Guid PuzzlePieceDefinitionId, int Quantity);
}