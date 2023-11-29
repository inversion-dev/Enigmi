using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.OrderAggregate.Events;

public record OrderCompleted(Guid OrderId) : DomainEvent;