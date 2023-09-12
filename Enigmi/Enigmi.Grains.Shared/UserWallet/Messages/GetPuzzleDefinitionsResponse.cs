namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record GetPuzzleDefinitionsResponse(IEnumerable<GetPuzzleDefinitionsResponse.PuzzleDefinition> PuzzleDefinitions)
{
    public record PuzzleDefinition(Guid Id, string Title, string CollectionTitle, int PuzzleSize,
        int NumberOfAllowedBuilds, int NumberOfCompletedBuilds, List<PuzzlePieceDefinition> PuzzlePieceDefinitions);

    public record PuzzlePieceDefinition(Guid Id, Guid PuzzleDefinitionId, string ImageUrl);
}
