namespace Enigmi.Grains.Shared.PuzzleDefinition.Messages;

public record CreatePuzzleDefinitionCommand(Guid PuzzleCollectionId, string ImageSourceBlobPath, string ConfigSourceBlobPath);

public record CreatePuzzleDefinitionResponse;