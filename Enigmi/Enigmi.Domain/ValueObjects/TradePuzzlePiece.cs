namespace Enigmi.Domain.ValueObjects;

public record TradePuzzlePiece(string PuzzlePieceId, Guid PuzzlePieceDefinitionId, Guid PuzzleDefinitionId, string PuzzleDefinitionTitle, Guid PuzzleCollectionId, string PuzzleCollectionTitle, 
    string StakingAddress, string Nickname, decimal Rating, 
    List<(Guid PuzzlePieceDefinitionId, int Quantity)> OutgoingPuzzleDefinitionPieceDefinitionInventory,
    List<(Guid PuzzlePieceDefinitionId, int Quantity)> IncomingPuzzleDefinitionPieceDefinitionInventory);