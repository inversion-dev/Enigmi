namespace Enigmi.Domain.ValueObjects;

public record TradeDetail(TradePuzzlePiece InitiatingPiece, TradePuzzlePiece CounterpartyPuzzlePiece, decimal Rating);