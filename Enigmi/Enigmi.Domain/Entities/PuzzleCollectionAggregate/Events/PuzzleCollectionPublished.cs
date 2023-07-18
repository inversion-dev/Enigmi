using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.PuzzleCollectionAggregate.Events;

public record PuzzleCollectionPublished(Guid PuzzleCollectionId) : DomainEvent;