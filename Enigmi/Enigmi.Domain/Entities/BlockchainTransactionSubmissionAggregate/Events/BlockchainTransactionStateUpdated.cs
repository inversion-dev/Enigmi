using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;

public record BlockchainTransactionStateUpdated(Guid OrderId, uint NumberOfConfirmations) : DomainEvent;