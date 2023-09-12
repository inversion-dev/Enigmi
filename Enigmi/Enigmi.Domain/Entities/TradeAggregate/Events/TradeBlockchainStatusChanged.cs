using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.TradeAggregate.Events;

public record TradeBlockchainStatusChanged(Guid TradeId) : DomainEvent;