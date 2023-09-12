using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.TradeAggregate.Events;

public record TradeSignedByInitiatedParty(Guid TradeId) : DomainEvent;