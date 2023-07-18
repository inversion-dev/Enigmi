namespace Enigmi.Domain.Entities.PuzzleCollectionAggregate.ValueObject;

public record PuzzlePolicy(string PolicyId, DateTime ClosingUtcDate, int? ClosingSlot);