using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.TradeAggregate.Events;

public record TradeCancelled(Guid TradeId) : DomainEvent;