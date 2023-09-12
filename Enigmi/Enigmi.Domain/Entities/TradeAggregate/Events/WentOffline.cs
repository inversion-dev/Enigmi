using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.TradeAggregate.Events;

public record WentOffline(Guid TradeId) : DomainEvent;