using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.OrderAggregate.Events;

public record OrderCancelled(Guid OrderId) : DomainEvent;