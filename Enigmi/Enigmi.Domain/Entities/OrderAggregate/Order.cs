using CardanoSharp.Wallet.Models.Addresses;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.OrderAggregate.Events;
using Enigmi.Domain.Entities.OrderAggregate.ValueObjects;
using Enigmi.Domain.ValueObjects;
using Newtonsoft.Json;
using static System.FormattableString;

namespace Enigmi.Domain.Entities.OrderAggregate;

public class Order : DomainEntity
{
    public Order(Guid id, TimeSpan expireTimeSpan)
    {
        Id = id.ThrowIfEmpty();
        ExpireTimeSpan = expireTimeSpan;
    }

    [JsonConstructor]
    private Order()
    {
        
    }
    
    [JsonProperty]
    public Guid Id { get; private set; }
    
    [JsonProperty]
    public ulong TransactionFeeInLovelace { get; private set; }

    [JsonProperty] 
    public OrderState State { get; private set; } = OrderState.Draft;

    [JsonProperty]
    public BlockchainTransaction? BlockchainTransaction { get; private set; }

    [JsonProperty]
    public string? UserWalletId { get; private set; }
    
    [JsonProperty]
    public int? OrderPuzzleSize { get; private set; }

    [JsonProperty]
    public Guid? OrderPuzzleCollectionId { get; private set; }

    [JsonProperty]
    public uint? NumberOfConfirmations { get; private set; }
    
    [JsonProperty]
    public TimeSpan ExpireTimeSpan { get; private set; }

    private List<TradeInPuzzlePiece> _tradeInPuzzlePieces = new List<TradeInPuzzlePiece>();

    [JsonProperty]
    public IEnumerable<TradeInPuzzlePiece> TradeInPuzzlePieces
    {
        get { return _tradeInPuzzlePieces.AsReadOnly(); }
        private set { _tradeInPuzzlePieces = value.ToList(); }
    }
    
    private List<OrderedPuzzlePiece> _orderedPuzzlePieces = new List<OrderedPuzzlePiece>();

    [JsonProperty]
    public IEnumerable<OrderedPuzzlePiece> OrderedPuzzlePieces
    {
        get { return _orderedPuzzlePieces.AsReadOnly(); }
        private set { _orderedPuzzlePieces = value.ToList(); }
    }

    public bool HasBeenSubmitted => State is OrderState.TransactionSubmitted;

    public bool HasOrderExpired() => (State == OrderState.TransactionCreated || State == OrderState.Draft)
                                   && CreatedUtcTimestamp <= DateTime.UtcNow.Subtract(ExpireTimeSpan);

    public bool IsOrderCancellationAllowed => State is OrderState.Draft or OrderState.TransactionCreated;

    public void AddOrderedPuzzlePieceId(string puzzlePieceId, string blockchainAssetId, string policyId, ulong priceInLovelace)
    {
        if (_orderedPuzzlePieces.Select(o => o.Id).Contains(puzzlePieceId))
            return;
        
        _orderedPuzzlePieces.Add(new OrderedPuzzlePiece(puzzlePieceId, blockchainAssetId, policyId, priceInLovelace));
    }

    public void AddTradeIdPuzzlePieceId(Guid puzzlePieceId, string blockchainAssetId, ulong tradeInValueInLovelace)
    {
        if (_tradeInPuzzlePieces.Select(o => o.Id).Contains(puzzlePieceId))
            return;
        _tradeInPuzzlePieces.Add(new TradeInPuzzlePiece(puzzlePieceId, blockchainAssetId, tradeInValueInLovelace));
    }

    public void SetOrderer(string userWalletId)
    {
        userWalletId.ThrowIfNullOrEmpty();
        UserWalletId = userWalletId;
    }

    public ResultOrError<CreateTransactionResponse> CreateTransaction(IEnumerable<UtxoAsset> userWalletAvailableAssets,
        uint latestSlot,
        DateTime latestSlotUtcTimestamp,
        CardanoNetworkParameters cardanoNetworkParameters,
        string systemAddress,
        string paymentAddress, int ttl,
        IEnumerable<PuzzlePieceMetadataList.PuzzlePieceMetadata> puzzlePieceMetadataList,
        string mnemonic, uint policyClosingSlot)
    {
        BlockchainTransaction = new BlockchainTransaction();
        var orderTotalInLovelace = (ulong)(_orderedPuzzlePieces.Sum(x => (long)x.PriceInLovelace));
        
        var createPaymentTransactionResponse = BlockchainTransaction.CreatePaymentTransaction(
                userWalletAvailableAssets, 
                latestSlot, 
                latestSlotUtcTimestamp,
                new Address(systemAddress),
                new Address(paymentAddress),
                cardanoNetworkParameters,
                orderTotalInLovelace,
                ttl,
                _orderedPuzzlePieces,
                puzzlePieceMetadataList.ThrowIfNullOrEmpty(),
                mnemonic.ThrowIfNullOrWhitespace(),
                policyClosingSlot);

        if (createPaymentTransactionResponse.HasErrors)
        {
            return createPaymentTransactionResponse.Errors.ToFailedResponse<CreateTransactionResponse>(); 
        }
            
        State = OrderState.TransactionCreated;
        return new CreateTransactionResponse(BlockchainTransaction.UnsignedTransactionCborHex!,  BlockchainTransaction.Fee!.Value).ToSuccessResponse();
    }

    public void CancelOrder()
    {
        State = OrderState.Cancelled;
        RaiseEvent(new OrderCancelled(Id));
    }

    public ResultOrError<ApproveOrderResponse> ApproveOrder(string cborWitnessHex)
    {
        if (BlockchainTransaction == null)
        {
            return "Blockchain transaction has not be created".ToFailedResponse<ApproveOrderResponse>();
        }

        if (State != OrderState.TransactionCreated)
        {
            return Invariant($"Transaction state was expected to be in created state, but is currently in {State}").ToFailedResponse<ApproveOrderResponse>();
        }

        if (BlockchainTransaction.UnsignedTransactionCborHex == null)
        {
            return "Blockchain transaction does not contain unsigned transaction".ToFailedResponse<ApproveOrderResponse>();
        }

        if (BlockchainTransaction.CreateSignedTransaction(cborWitnessHex))
        {
            State = OrderState.TransactionSigned;
            return new ApproveOrderResponse().ToSuccessResponse();
        }

        return "Unexpected failure to sign transaction".ToFailedResponse<ApproveOrderResponse>();
    }

    public void SetOrderMetadata(Guid puzzleCollectionId, int puzzleSize)
    {
        puzzleCollectionId.ThrowIfEmpty();
        puzzleSize.ThrowIf(x => x <= 0);
        
        OrderPuzzleCollectionId = puzzleCollectionId;
        OrderPuzzleSize = puzzleSize;
    }

    public void CompleteOrder()
    {
        State = OrderState.Completed;
    }

    public void MarkAsSubmitted()
    {
        State = OrderState.TransactionSubmitted;
    }

    public void UpdateNumberOfConfirmations(uint numberOfConfirmations)
    {
        NumberOfConfirmations = numberOfConfirmations;
    }
}