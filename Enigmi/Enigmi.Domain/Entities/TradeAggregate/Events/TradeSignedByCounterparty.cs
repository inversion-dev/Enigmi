using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.TradeAggregate.Events;

public record TradeSignedByCounterparty(Guid TradeId) : DomainEvent;