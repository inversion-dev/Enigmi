namespace Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;

public record UserWalletPuzzlePiece(String StakingAddress, string PuzzlePieceId, Guid PuzzleDefinitionId, Guid PuzzlePieceDefinitionId);