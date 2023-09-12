using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;

public record BlockchainTransactionFailed(BlockchainTransactionProcessState State, bool IsDoubleSpent) : DomainEvent;