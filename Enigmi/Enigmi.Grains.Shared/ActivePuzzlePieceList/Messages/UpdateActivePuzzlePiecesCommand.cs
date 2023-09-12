namespace Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;

public record UpdateActivePuzzlePiecesCommand(string StakingAddress, string Nickname, List<UpdateActivePuzzlePiecesCommand.PuzzlePiece> PuzzlePieces)
{
    public record PuzzlePiece(
        string PuzzlePieceId,
        Guid PuzzlePieceDefinitionId,
        Guid PuzzleDefinitionId);
}

public record UpdateActivePuzzlePiecesResponse();
    