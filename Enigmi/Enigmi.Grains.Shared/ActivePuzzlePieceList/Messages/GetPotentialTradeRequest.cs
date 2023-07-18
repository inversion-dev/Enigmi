namespace Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;

public record GetPotentialTradeRequest(List<TradeDetail> TradeDetails);

public record TradeDetail(TradePuzzlePiece OwnedPuzzlePiece, TradePuzzlePiece TradePuzzlePiece, decimal Rating);

public record TradePuzzlePiece(string PuzzlePieceId, Guid PuzzleDefinitionId, string PuzzleDefinitionTitle, Guid PuzzleCollectionId, string PuzzleCollectionTitle, string StakingAddress, decimal Rating);
