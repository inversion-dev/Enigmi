﻿using CardanoSharp.Wallet.Models.Addresses;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.TradeAggregate.Events;
using Enigmi.Domain.ValueObjects;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.TradeAggregate;

public class Trade : DomainEntity
{
    public Trade(Guid id, TradeDetail detail, TimeSpan initiatingPartySignTimespanDeadline)
	{
		State = TradeState.New;
		TradeDetail = detail.ThrowIfNull();
		Id = id.ThrowIfEmpty();
		InitiatingPartySignatureDeadlineTimespan = initiatingPartySignTimespanDeadline;

		RaiseEvent(new TradeCreated(id));
	}

    [JsonConstructor]
    private Trade()
    {
        
    }

    
    [JsonProperty]
    private List<Utxo> ExternalReservedUtxos { get; set; } = new();
    
    public void AddExternalReservedUtxo(List<Utxo> utxos)
    {
	    foreach (var utxo in utxos)
	    {
		    if (ExternalReservedUtxos.Any(x => x.TxId == utxo.TxId && x.OutputIndexOnTx == utxo.OutputIndexOnTx))
		    {
			    continue;
		    }
	    
		    ExternalReservedUtxos.Add(utxo);
	    }
    }

    public void ClearExternalReservedUtxos()
    {
	    ExternalReservedUtxos.Clear();
    }

    public void RemoveExternalReservedUtxo(Utxo utxo)
    {
		ExternalReservedUtxos.Remove(utxo);
    }
    
    [JsonProperty]
    public Guid Id { get; private set; }

	[JsonProperty]
	public TradeState State { get; set; }


	public bool IsAvailable => !ExternalReservedUtxos.Any();

	[JsonProperty] 
    public TradeDetail TradeDetail { get; set; } = null!;
    
    [JsonProperty] 
    public BlockchainTransaction? BlockchainTransaction { get; private set; } = null!;
    
    [JsonProperty]
    public TimeSpan InitiatingPartySignatureDeadlineTimespan { get; private set; }
    
    [JsonProperty]
    public DateTime? InitiatingPartySignUtcDeadline { get; private set; }

    [JsonProperty] 
    public uint NumberOfConfirmations { get; private set; } = 0;
    
    private List<Utxo> _puzzlePieceRelatedUtxos = new List<Utxo>();

    [JsonProperty] 
    public IEnumerable<Utxo> PuzzlePieceRelatedUtxos
    {
	    get => _puzzlePieceRelatedUtxos.AsReadOnly();
	    set => _puzzlePieceRelatedUtxos = value.ToList();
    }
    
    public void CancelTrade()
    {
	    ClearExternalReservedUtxos();
        State = TradeState.Cancelled;
        RaiseEvent(new TradeCancelled(Id));
    }

    public void WentOffline()
    {
	    CancelTrade();
	    RaiseEvent(new WentOffline(Id));
    }
    
    public ResultOrError<Enigmi.Constants.Unit> SignByCounterparty(string counterPartyWitnessCborHex)
    {
	    counterPartyWitnessCborHex.ThrowIfNullOrWhitespace();
	    BlockchainTransaction.ThrowIfNull();

	    if (State != TradeState.New && State != TradeState.CounterpartySigned)
	    {
		   return $"Trade is not in the correct state to sign, current state is {State}".ToFailedResponse<Enigmi.Constants.Unit>();
	    }

	    BlockchainTransaction.SignByCounterparty(counterPartyWitnessCborHex);
	    State = TradeState.CounterpartySigned;
	    InitiatingPartySignUtcDeadline = DateTime.UtcNow.Add(InitiatingPartySignatureDeadlineTimespan);
	    
	    RaiseEvent(new TradeSignedByCounterparty(Id));

	    return new Enigmi.Constants.Unit().ToSuccessResponse();
	}

    public ResultOrError<Enigmi.Constants.Unit> SignByInitiatingParty(string initiatingPartyWitnessCborHex)
    {
	    initiatingPartyWitnessCborHex.ThrowIfNullOrWhitespace();
	    BlockchainTransaction.ThrowIfNull();
	    
	    if (State != TradeState.CounterpartySigned)
	    {
		    return $"Trade is not in the correct state to sign, current state is {State}".ToFailedResponse<Enigmi.Constants.Unit>();
	    }
	    
	    var signByInitiatingPartyResponse = BlockchainTransaction.SignByInitiatingParty(initiatingPartyWitnessCborHex);
	    if (signByInitiatingPartyResponse.HasErrors)
	    {
		    return signByInitiatingPartyResponse.Errors.ToFailedResponse<Enigmi.Constants.Unit>();
	    }
	    
	    State = TradeState.FullySigned;
		RaiseEvent(new TradeSignedByInitiatedParty(Id));
	    return new Enigmi.Constants.Unit().ToSuccessResponse();
    }

    public ResultOrError<Enigmi.Constants.Unit> CreateTradeTransaction(
	    IEnumerable<UtxoAsset> initiatingWalletAvailableAssets,
	    IEnumerable<UtxoAsset> counterpartyWalletAvailableAssets,
	    uint latestSlot,
	    DateTime latestSlotUtcTimestamp,
	    Address initiatingAddress,
	    Address counterAddress,
	    CardanoNetworkParameters networkParams,
	    int ttl,
	    UtxoAsset initiatingPuzzlePieceAsset,
	    UtxoAsset counterpartyPuzzlePieceAsset)
    {
	    BlockchainTransaction = new BlockchainTransaction();
	    
	    if (State != TradeState.New && State != TradeState.CounterpartySigned)
	    {
		    return $"Trade is not in the correct state to sign, current state is {State}".ToFailedResponse<Enigmi.Constants.Unit>();
	    }
	    
	    var createTradeTransactionResponse = BlockchainTransaction.CreateTradeTransaction(initiatingWalletAvailableAssets, 
		    counterpartyWalletAvailableAssets,
		    latestSlot,
		    latestSlotUtcTimestamp,
		    initiatingAddress,
		    counterAddress,
		    networkParams,
		    ttl,
		    initiatingPuzzlePieceAsset,
		    counterpartyPuzzlePieceAsset);
	    
	    if (createTradeTransactionResponse.HasErrors)
	    {
		    return createTradeTransactionResponse.Errors.ToFailedResponse<Enigmi.Constants.Unit>();
	    }

	    return new Enigmi.Constants.Unit().ToSuccessResponse();
    }

    public void BlockchainTransactionFailed()
    {
	    State = TradeState.SubmissionFailed;
	    RaiseEvent(new TradeBlockchainSubmissionFailed(Id));
    }

    public void MarkAsCompleted()
    {
	    State = TradeState.Completed;
	    RaiseEvent(new TradeCompleted(Id));
    }

    public void UpdateNumberOfConfirmations(uint eventNumberOfConfirmations)
    {
	    if (NumberOfConfirmations == eventNumberOfConfirmations)
	    {
		    return;
	    }
	    
	    NumberOfConfirmations = eventNumberOfConfirmations; 
	    RaiseEvent(new TradeBlockchainStatusChanged(Id));
    }

    public void MarkAsSubmitted()
    {
	    State = TradeState.Submitted;
	    RaiseEvent(new TradeBlockchainStatusChanged(Id));
    }

    public void SetPuzzlePieceUtxos(IEnumerable<Utxo> relatedUtxos)
    {
	    relatedUtxos.ThrowIfNullOrEmpty();
	    PuzzlePieceRelatedUtxos = relatedUtxos;
    }
}