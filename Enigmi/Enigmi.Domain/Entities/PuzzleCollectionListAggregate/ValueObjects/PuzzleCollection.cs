namespace Enigmi.Domain.Entities.PuzzleCollectionListAggregate.ValueObjects;

public record PuzzleCollection(Guid Id, string Title, string CoverImageBlobPath, List<int> AvailableSizes, decimal PuzzlePiecePriceInAda);

