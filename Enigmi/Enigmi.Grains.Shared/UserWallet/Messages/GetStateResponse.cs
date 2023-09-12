namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record GetStateResponse(List<GetStateResponse.PuzzlePiece> PuzzlePieces, List<GetStateResponse.PuzzleDefinition> PuzzleDefinitions)
{
    public record PuzzlePiece(Guid PuzzlePieceDefinitionId, Guid PuzzleDefinitionId, String ImageUrl, int X, int Y, List<string> OwnedPuzzlePieceIds, int PuzzlePieceCount)
    {
        public bool IsOwned => OwnedPuzzlePieceIds.Any();
    };

    public record PuzzleDefinition(Guid Id, string Title, string CollectionTitle, int PuzzleSize, int NumberOfAllowedBuilds, int NumberOfCompletedBuilds);
}