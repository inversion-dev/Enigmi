
using Enigmi.Domain.ValueObjects;

namespace Enigmi.Domain.Entities.PuzzleCollectionAggregate.ValueObject;

public record PuzzleDefinition(Guid Id)
{
    public ActivationStatus State { get; set; }
};
