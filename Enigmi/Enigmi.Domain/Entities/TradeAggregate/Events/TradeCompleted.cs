using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.TradeAggregate.Events;

public record TradeCompleted(Guid TradeId) : DomainEvent;