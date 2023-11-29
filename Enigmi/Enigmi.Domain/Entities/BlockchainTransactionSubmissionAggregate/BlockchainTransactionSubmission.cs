using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate;
 
public class BlockchainTransactionSubmission : DomainEntity
{
    private static readonly Dictionary<BlockchainTransactionProcessState, List<BlockchainTransactionProcessState>> StateMachine = new();

    static BlockchainTransactionSubmission()
    {
        InitializeStatusStateMachine();
    }

    public BlockchainTransactionSubmission()
    {
        State = BlockchainTransactionProcessState.UnSubmitted;
    }

    [JsonProperty]
    public Guid SubscriptionKey { get; private set; }
    
    [JsonProperty]
    public string? TransactionId { get; private set; }
    
    [JsonProperty]
    public string? SignedTransactionCborHex { get; private set; }
    
    [JsonProperty]
    public DateTime? TtlUtcTimestamp { get; private set; }

    [JsonProperty]
    public BlockchainTransactionProcessState State { get; private set; }
    
    [JsonProperty]
    public int SubmissionTryCount { get; private set; }

    public void SetSubscriptionKey(Guid subscriptionKey)
    {
        SubscriptionKey = subscriptionKey.ThrowIfEmpty();
    }
    public void SetTransactionId(string transactionId)
    {
        TransactionId = transactionId.ThrowIfNullOrWhitespace();
    }

    public bool ShouldSync =>
        new List<BlockchainTransactionProcessState>()
        {
            BlockchainTransactionProcessState.UnSubmitted,
            BlockchainTransactionProcessState.Submitted,
            BlockchainTransactionProcessState.SubmissionTransientFailure,
            BlockchainTransactionProcessState.OnChain,
            BlockchainTransactionProcessState.TransientRejected,
        }.Contains(State);
    
    public bool ShouldResubmitTransaction =>
        new List<BlockchainTransactionProcessState>()
        {
            BlockchainTransactionProcessState.UnSubmitted,
            BlockchainTransactionProcessState.SubmissionTransientFailure,
            BlockchainTransactionProcessState.TransientRejected,
        }.Contains(State);
    
    public void MarkAsNotIncluded()
    {
        if (!IsTransitionAllowed(BlockchainTransactionProcessState.NotIncluded))
        {
            return;
        }
        
        State = BlockchainTransactionProcessState.NotIncluded;
        RaiseEvent(new BlockchainTransactionFailed(State, false));
    }

    private bool IsTransitionAllowed(BlockchainTransactionProcessState newState)
    {
        var allowedStatuses = StateMachine[State];
        return allowedStatuses.Contains(newState);
    }

    private static void InitializeStatusStateMachine()
    {
        StateMachine.Add(BlockchainTransactionProcessState.UnSubmitted, new List<BlockchainTransactionProcessState>
        {
            BlockchainTransactionProcessState.Submitted,
            BlockchainTransactionProcessState.SubmissionTransientFailure,
            BlockchainTransactionProcessState.OnChain,
            BlockchainTransactionProcessState.Rejected,
            BlockchainTransactionProcessState.TransientRejected,
            BlockchainTransactionProcessState.OnChainConfirmed,
            BlockchainTransactionProcessState.NotIncluded,
        });
        StateMachine.Add(BlockchainTransactionProcessState.TransientRejected, new List<BlockchainTransactionProcessState>
        {
            BlockchainTransactionProcessState.Submitted,
            BlockchainTransactionProcessState.SubmissionTransientFailure,
            BlockchainTransactionProcessState.OnChain,
            BlockchainTransactionProcessState.Rejected,
            BlockchainTransactionProcessState.OnChainConfirmed,
            BlockchainTransactionProcessState.NotIncluded,
        });
        StateMachine.Add(BlockchainTransactionProcessState.Submitted, new List<BlockchainTransactionProcessState>
        {
            BlockchainTransactionProcessState.OnChain,
            BlockchainTransactionProcessState.OnChainConfirmed,
            BlockchainTransactionProcessState.NotIncluded,
        });
        StateMachine.Add(BlockchainTransactionProcessState.SubmissionTransientFailure,
            new List<BlockchainTransactionProcessState>
            {
                BlockchainTransactionProcessState.Submitted,
                BlockchainTransactionProcessState.OnChain,
                BlockchainTransactionProcessState.Rejected,
                BlockchainTransactionProcessState.TransientRejected,
                BlockchainTransactionProcessState.OnChainConfirmed,
                BlockchainTransactionProcessState.NotIncluded,
            });
        StateMachine.Add(BlockchainTransactionProcessState.OnChain, new List<BlockchainTransactionProcessState>
        {
            BlockchainTransactionProcessState.OnChainConfirmed
        });
        StateMachine.Add(BlockchainTransactionProcessState.Rejected, new List<BlockchainTransactionProcessState>());
        StateMachine.Add(BlockchainTransactionProcessState.OnChainConfirmed, new List<BlockchainTransactionProcessState>());
        StateMachine.Add(BlockchainTransactionProcessState.NotIncluded, new List<BlockchainTransactionProcessState>());
    }

    public void MarkAsOnChainConfirmed(string transactionTxId, uint transactionBlockHeight, string transactionBlockHash, DateTime transactionBlockUtcTimestamp)
    {
        if (!IsTransitionAllowed(BlockchainTransactionProcessState.OnChainConfirmed))
        {
            return;
        }
        
        State = BlockchainTransactionProcessState.OnChainConfirmed;
        RaiseEvent(new BlockchainTransactionSucceeded(
            transactionTxId.ThrowIfNullOrWhitespace(), 
            transactionBlockHeight, 
            transactionBlockHash.ThrowIfNullOrWhitespace(), 
            transactionBlockUtcTimestamp)
        );
    }

    public void MarkAsOnChain(uint confirmationCount)
    {
        RaiseEvent(new BlockchainTransactionStateUpdated(SubscriptionKey, confirmationCount));
        if (!IsTransitionAllowed(BlockchainTransactionProcessState.OnChain))
        {
            return;
        }
        
        State = BlockchainTransactionProcessState.OnChain;
    }

    public void MarkAsSubmissionTransientFailure()
    {
        if (!IsTransitionAllowed(BlockchainTransactionProcessState.SubmissionTransientFailure))
        {
            return;
        }
        
        State = BlockchainTransactionProcessState.SubmissionTransientFailure;
    }

    public void MarkAsRejected(int maxTransientRejectedCount, bool isDoubleSpent)
    {
        maxTransientRejectedCount.ThrowIf(x => x < 0);
        
        SubmissionTryCount++;
        if (SubmissionTryCount < maxTransientRejectedCount)
        {
            if (!IsTransitionAllowed(BlockchainTransactionProcessState.TransientRejected))
            {
                return;
            }
            
            State = BlockchainTransactionProcessState.TransientRejected;
        }
        else
        {
            if (!IsTransitionAllowed(BlockchainTransactionProcessState.Rejected))
            {
                return;
            }

            State = BlockchainTransactionProcessState.Rejected;
            RaiseEvent(new BlockchainTransactionFailed(State, isDoubleSpent));
        }
    }

    public void MarkAsSubmitted()
    {
        if (!IsTransitionAllowed(BlockchainTransactionProcessState.Submitted))
        {
            return;
        }
    
        RaiseEvent(new BlockchainTransactionSubmitted());
        State = BlockchainTransactionProcessState.Submitted;
    }

    public void SetTransactionDetails(string signedTransactionCborHex, DateTime ttlUtcTimestamp)
    {
        SignedTransactionCborHex = signedTransactionCborHex.ThrowIfNullOrWhitespace();
        TtlUtcTimestamp = ttlUtcTimestamp.ThrowIfNull();
    }
}