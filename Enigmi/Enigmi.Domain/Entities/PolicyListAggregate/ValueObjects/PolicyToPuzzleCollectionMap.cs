namespace Enigmi.Domain.Entities.PolicyListAggregate.ValueObjects;

public record PolicyToPuzzleCollectionMap(Guid PuzzleCollectionId, string PolicyId, PolicyType PolicyType)
{
    public PolicyStatus PolicyStatus { get; set; } = PolicyStatus.Adding;
}