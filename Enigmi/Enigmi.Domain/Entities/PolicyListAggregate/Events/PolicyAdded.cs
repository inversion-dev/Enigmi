using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.PolicyListAggregate.Events;

public record PolicyAdded(PolicyType PolicyType, string PolicyId) : DomainEvent;