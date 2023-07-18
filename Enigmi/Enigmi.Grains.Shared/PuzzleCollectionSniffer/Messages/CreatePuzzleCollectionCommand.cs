namespace Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;

public record SeedPuzzleCollectionCommand(string DropSourceBlobFolderPath);

public record SeedPuzzleCollectionResponse(Guid PuzzleCollectionId);

