namespace Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate;

public enum BlockchainTransactionProcessState : byte
{
    UnSubmitted = 0,
    
    Submitted = 1,
    
    SubmissionTransientFailure = 2,
    
    OnChain = 3,

    Rejected = 4,

    OnChainConfirmed = 5,

    NotIncluded = 6,

    TransientRejected = 7
}