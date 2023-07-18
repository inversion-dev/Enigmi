using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.PolicyAggregate.Events;

public record PolicyCreated(String PolicyId) : DomainEvent;