using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.PuzzleDefinitionAggregate.Events;

public record PuzzleDefinitionPublished(Guid PuzzleDefinitionId) : DomainEvent;