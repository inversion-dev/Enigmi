using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.PuzzleCollectionAggregate.Events;

public record PuzzleCollectionCreated(Guid PuzzleCollectionId, DateTime PuzzlePolicyClosingUtcDate, DateTime PuzzlePiecePolicyClosingUtcDate) : DomainEvent;