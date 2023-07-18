namespace Enigmi.Grains.Shared.PuzzlePiece.Messages;

public record CreatePuzzlePieceCommand(Guid PuzzlePieceDefinitionId, Guid PuzzleDefinitionId, string BlockchainAssetId);

public record CreatePuzzlePieceResponse;
