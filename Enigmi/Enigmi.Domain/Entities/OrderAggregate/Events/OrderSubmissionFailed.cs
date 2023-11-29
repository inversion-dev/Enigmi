using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.OrderAggregate.Events;

public record OrderSubmissionFailed(Guid OrderId) : DomainEvent;