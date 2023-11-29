using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Extensions.Models.Transactions.TransactionWitnesses;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.TransactionBuilding;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Common.Utils;
using Enigmi.Domain.Utils;
using Enigmi.Domain.ValueObjects;
using Newtonsoft.Json;
using Utxo = CardanoSharp.Wallet.Models.Utxo;

namespace Enigmi.Domain.Entities.TradeAggregate;

public class BlockchainTransaction
{
	[JsonProperty] 
	public string? UnsignedTransactionCborHex { get; private set; }
    
	[JsonProperty] 
	public DateTime? TtlUtcTimestamp { get; private set; }
    
	[JsonProperty]
	public string? TransactionId { get; set; }
    
	[JsonProperty]
	public uint? Fee { get; set; }
    
	[JsonProperty]
	public string? CounterPartyWitnessCborHex { get; private set; }
    
	[JsonProperty]
	public string? SignedTransactionCborHex { get; private set; }
	
	public List<Utxo>? CounterPartyUsedUtxos { get; set; }

	public List<Utxo>? InitiatingPartyUsedUtxos { get; set; }
	
	public ResultOrError<Enigmi.Constants.Unit> SignByInitiatingParty(string initiatingPartyWitnessCborHex)
	{
		initiatingPartyWitnessCborHex.ThrowIfNullOrWhitespace();

		var currentTx = UnsignedTransactionCborHex.HexToByteArray().DeserializeTransaction();
		var counterpartyWitness = CounterPartyWitnessCborHex.HexToByteArray().DeserializeTransactionWitnessSet();
		var initiatingPartyWitness = initiatingPartyWitnessCborHex.HexToByteArray().DeserializeTransactionWitnessSet();
        
		currentTx.TransactionWitnessSet ??= new TransactionWitnessSet();
		foreach (var keyWitness in counterpartyWitness.VKeyWitnesses)
		{
			currentTx.TransactionWitnessSet.VKeyWitnesses.Add(keyWitness);
		}
		foreach (var keyWitness in initiatingPartyWitness.VKeyWitnesses)
		{
			currentTx.TransactionWitnessSet.VKeyWitnesses.Add(keyWitness);
		}

		SignedTransactionCborHex = Convert.ToHexString(currentTx.Serialize());

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
		initiatingWalletAvailableAssets.ThrowIfNull();
		counterpartyWalletAvailableAssets.ThrowIfNull();
		initiatingAddress.ThrowIfNull();
		counterAddress.ThrowIfNull();
		networkParams.ThrowIfNull();
		latestSlot.ThrowIf(x => x <= 0);
		ttl.ThrowIf(x => x <= 0);
		initiatingPuzzlePieceAsset.ThrowIfNull();
		counterpartyPuzzlePieceAsset.ThrowIfNull();
		

		long transactionTtl = latestSlot + ttl;

		var transactionBodyBuilder = TransactionBodyBuilder.Create
			.SetFee(0)
			.SetTtl(Convert.ToUInt32(transactionTtl));

		var initiatingWalletUtxos = UtxoUtility.ConvertToCardanoSharpUtxos(initiatingWalletAvailableAssets);
		var counterPartyWalletUtxos = UtxoUtility.ConvertToCardanoSharpUtxos(counterpartyWalletAvailableAssets);
		
		var oneAdaLovelacePlusBuffer = Convert.ToDecimal(1.5 * Enigmi.Constants.LovelacePerAda);
		
		ITokenBundleBuilder initiatingBundleBuilder = TokenBundleBuilder.Create;
		ITokenBundleBuilder counterpartyBundleBuilder = TokenBundleBuilder.Create;

		var initiatingUtxosResponse = GetUtxosToUse(initiatingPuzzlePieceAsset, initiatingWalletUtxos, oneAdaLovelacePlusBuffer);
		if (initiatingUtxosResponse.HasErrors)
		{
			return initiatingUtxosResponse.Errors.ToFailedResponse<Enigmi.Constants.Unit>();
		}
		var initiatingUtxos = initiatingUtxosResponse.Result!;
		var initiatingLoveLaceTotal = AddTokensToBundleBuilder(initiatingPuzzlePieceAsset, initiatingUtxos, counterpartyBundleBuilder, initiatingBundleBuilder);
		
		var counterpartyUtxosResponse = GetUtxosToUse(counterpartyPuzzlePieceAsset, counterPartyWalletUtxos, oneAdaLovelacePlusBuffer);
		if (counterpartyUtxosResponse.HasErrors)
		{
			return counterpartyUtxosResponse.Errors.ToFailedResponse<Enigmi.Constants.Unit>();
		}
		var counterPartyUtxos = counterpartyUtxosResponse.Result!;
		var counterPartyLoveLaceTotal = AddTokensToBundleBuilder(counterpartyPuzzlePieceAsset, counterPartyUtxos, initiatingBundleBuilder,counterpartyBundleBuilder);
		
		foreach (var utxo in initiatingUtxos)
		{
			transactionBodyBuilder = transactionBodyBuilder.AddInput(utxo.TxHash, utxo.TxIndex);
		}
		foreach (var utxo in counterPartyUtxos)
		{
			transactionBodyBuilder = transactionBodyBuilder.AddInput(utxo.TxHash, utxo.TxIndex);
		}
		
		InitiatingPartyUsedUtxos = initiatingUtxos;
		CounterPartyUsedUtxos = counterPartyUtxos;
		
		transactionBodyBuilder.AddOutput(initiatingAddress, initiatingLoveLaceTotal, initiatingBundleBuilder);
		transactionBodyBuilder.AddOutput(counterAddress, counterPartyLoveLaceTotal, counterpartyBundleBuilder);
		var transaction = BuildTransactionWithFee(transactionBodyBuilder, networkParams, null, null);
		
		UnsignedTransactionCborHex = Convert.ToHexString(transaction.Serialize());
		TtlUtcTimestamp = latestSlotUtcTimestamp.AddSeconds(ttl);
		TransactionId = UtxoUtility.GetTransactionId(transaction);

		return new Enigmi.Constants.Unit().ToSuccessResponse();
	}

	private static ResultOrError<List<CardanoSharp.Wallet.Models.Utxo>> GetUtxosToUse(UtxoAsset initiatingPuzzlePieceAsset, List<CardanoSharp.Wallet.Models.Utxo> initiatingWalletUtxos,
	    decimal oneAdaLovelacePlusBuffer)
    {
	    var initiatingUtxos = new List<CardanoSharp.Wallet.Models.Utxo>();

	    if (!initiatingWalletUtxos.Any())
	    {
		    return "Not enough funds to complete the transaction".ToFailedResponse<List<CardanoSharp.Wallet.Models.Utxo>>();
	    }
	    
	    var initiatingUtxoMustBeIncluded = initiatingWalletUtxos
		    .SingleOrDefault(x =>
			    x.TxHash == initiatingPuzzlePieceAsset.TxId && x.TxIndex == initiatingPuzzlePieceAsset.OutputIndexOnTx);

	    if (initiatingUtxoMustBeIncluded == null)
	    {
		    return "Utxo containing the required asset is not available".ToFailedResponse<List<CardanoSharp.Wallet.Models.Utxo>>();
	    }

	    initiatingUtxos.Add(initiatingUtxoMustBeIncluded);

	    while (initiatingUtxos.Sum(x => Convert.ToDecimal(x.Balance.Lovelaces)) < oneAdaLovelacePlusBuffer)
	    {
		    var utxo = initiatingWalletUtxos
			    .Where(x => !initiatingUtxos.Contains(x))
			    .OrderByDescending(x => x.Balance.Lovelaces)
			    .FirstOrDefault();

		    if (utxo == null)
		    {	
			    return "Not enough funds to complete the transaction".ToFailedResponse<List<CardanoSharp.Wallet.Models.Utxo>>();
		    }

		    initiatingUtxos.Add(utxo);
	    }
		
	    return initiatingUtxos.ToSuccessResponse();
    }

    private static ulong AddTokensToBundleBuilder(UtxoAsset initiatingPuzzlePieceAsset, List<CardanoSharp.Wallet.Models.Utxo> initiatingUtxos,
	    ITokenBundleBuilder counterpartyBundleBuilder, ITokenBundleBuilder initiatingBundleBuilder)
    {
	    ulong initiatingLoveLaceTotal = 0;
	    foreach (var utxo in initiatingUtxos)
	    {
		    foreach (var asset in utxo.Balance.Assets)
		    {
			    var assetId = Convert.FromHexString(asset.PolicyId).ToHexStringLower() + Convert.FromHexString(asset.Name).ToHexStringLower();
			    if (assetId == initiatingPuzzlePieceAsset.BlockchainAssetId)
			    {
				    counterpartyBundleBuilder.AddToken(Convert.FromHexString(asset.PolicyId),
					    Convert.FromHexString(asset.Name), asset.Quantity);
			    }
			    else
			    {
				    initiatingBundleBuilder.AddToken(Convert.FromHexString(asset.PolicyId),
					    Convert.FromHexString(asset.Name), asset.Quantity);
			    }
		    }

		    initiatingLoveLaceTotal += utxo.Balance.Lovelaces;
	    }

	    return initiatingLoveLaceTotal;
    }
    
    private Transaction BuildTransactionWithFee(
        ITransactionBodyBuilder transactionBodyBuilder,
        CardanoNetworkParameters networkParams,
        ITransactionWitnessSetBuilder? witnesses,
        IAuxiliaryDataBuilder? auxiliaryDataBuilder)
    {
        var transactionBuilder = TransactionBuilder.Create
            .SetBody(transactionBodyBuilder);

        if (witnesses != null)
        {
            transactionBuilder.SetWitnesses(witnesses);
        }

        Transaction transaction = transactionBuilder.Build();
        if (auxiliaryDataBuilder != null)
        {
            transactionBuilder.SetAuxData(auxiliaryDataBuilder);	
        }

        uint fee = transaction.CalculateFee(networkParams.MinFeeA, networkParams.MinFeeB);
		
        Fee = (uint)(fee * 1.1 /*NetworkFeeMultiplier*/);
		
        transactionBodyBuilder.SetFee((ulong)Fee);
        transaction = transactionBuilder.Build();
        transaction.TransactionBody.TransactionOutputs.First().Value.Coin -= Fee!.Value;

        return transaction;
    }

    public void SignByCounterparty(string counterPartyWitnessCborHex)
    {
	    CounterPartyWitnessCborHex = counterPartyWitnessCborHex;
    }
}