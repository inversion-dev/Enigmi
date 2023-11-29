using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.TradeAggregate.Events;

public record TradeBlockchainSubmissionFailed(Guid TradeId) : DomainEvent;