using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;

public record BlockchainTransactionSucceeded(string TransactionTxId, uint TransactionBlockHeight, string TransactionBlockHash, DateTime TransactionBlockUtcTimestamp) : DomainEvent;