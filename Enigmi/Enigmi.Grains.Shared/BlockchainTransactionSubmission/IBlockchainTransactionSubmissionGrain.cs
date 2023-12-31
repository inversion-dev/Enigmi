﻿using Enigmi.Common.Messaging;

namespace Enigmi.Grains.Shared.BlockchainTransactionSubmission;

public interface IBlockchainTransactionSubmissionGrain : IGrainWithGuidKey
{
    Task<ResultOrError<Constants.Unit>> Submit(Guid subscriptionKey, string signedTransactionCborHex, DateTime blockchainTransactionTtlUtcTimestamp);

    ValueTask<Domain.Entities.BlockchainTransactionSubmissionAggregate.BlockchainTransactionSubmission> GetBlockchainTransactionSubmissionDetail();
}