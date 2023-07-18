using System.Text;
using CardanoSharp.Wallet;
using CardanoSharp.Wallet.CIPs.CIP2;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Extensions.Models.Transactions.TransactionWitnesses;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.Scripts;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Common.Utils;
using Enigmi.Domain.Entities.OrderAggregate.ValueObjects;
using Enigmi.Domain.ValueObjects;
using Newtonsoft.Json;
using PeterO.Cbor2;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;
using Utxo = CardanoSharp.Wallet.Models.Utxo;

namespace Enigmi.Domain.Entities.OrderAggregate;

public class BlockchainTransaction
{
    [JsonProperty]
    public Guid Id { get; private set; }

    [JsonProperty]
    public string? UnsignedTransactionCborHex { get; private set; }

	[JsonProperty]
    public string? SignedTransactionCborHex { get; private set; }
    
    [JsonProperty]
    public string? TransactionId { get; private set; }
    
    [JsonProperty]
    public uint? Fee { get; private set; }

    private List<UtxoAsset> _userWalletInputBlockchainAssets = new List<UtxoAsset>();

    [JsonProperty]
    public IEnumerable<UtxoAsset> UserWalletInputBlockchainAssets
	{
		get { return _userWalletInputBlockchainAssets.AsReadOnly(); }
		private set { _userWalletInputBlockchainAssets = value.ToList(); }
	}
    
    [JsonProperty]
    public DateTime? TtlUtcTimestamp { get; private set; }

	public ResultOrError<Enigmi.Constants.Unit> CreatePaymentTransaction(
		IEnumerable<UtxoAsset> userWalletAvailableAssets,
		uint latestSlot,
		DateTime latestSlotUtcTimestamp,
		Address paymentToAddress,
		Address paymentFromAddress,
		CardanoNetworkParameters networkParams,
		ulong orderTotalInLovelace,
		int ttl,
		List<OrderedPuzzlePiece> orderedPuzzlePieces,
		IEnumerable<PuzzlePieceMetadataList.PuzzlePieceMetadata> puzzlePieceMetadataList,
		string mnemonic, 
		uint policyClosingSlot)
	{
		userWalletAvailableAssets.ThrowIfNullOrEmpty();
		paymentToAddress.ThrowIfNull();
		paymentFromAddress.ThrowIfNull();
		networkParams.ThrowIfNull();
		mnemonic.ThrowIfNullOrWhitespace();
		puzzlePieceMetadataList.ThrowIfNullOrEmpty();
		
		long transactionTtl = latestSlot + ttl;

		var transactionBodyBuilder = TransactionBodyBuilder.Create
			.AddOutput(paymentToAddress, orderTotalInLovelace)
			.SetFee(0)
			.SetTtl(Convert.ToUInt32(transactionTtl));

		var allWalletUtxos = ConvertToCardanoSharpUtxos(userWalletAvailableAssets);

		ITokenBundleBuilder tokenBundleBuilder = TokenBundleBuilder.Create;
		ITokenBundleBuilder changeBundleBuilder = TokenBundleBuilder.Create;
		
		var auxDataBuilder = BuildMetadataAndMintingTokens(orderedPuzzlePieces, puzzlePieceMetadataList, tokenBundleBuilder, changeBundleBuilder);

		transactionBodyBuilder.SetMint(tokenBundleBuilder);  
		  
		try
		{
			var coinSelection = ((TransactionBodyBuilder)transactionBodyBuilder).UseRandomImprove(allWalletUtxos,
				paymentFromAddress.ToString(), tokenBundleBuilder, (int)orderTotalInLovelace, networkParams.MinFeeB);

			foreach(var changeOutput in coinSelection.ChangeOutputs)
			{
				foreach (var asset in changeOutput.Value.MultiAsset)
				{
					foreach (var token in asset.Value.Token)
					{
						changeBundleBuilder.AddToken(asset.Key, token.Key, asset.Value.Token[token.Key]);
					}
				}

				transactionBodyBuilder.AddOutput(changeOutput.Address, changeOutput.Value.Coin, changeBundleBuilder, OutputPurpose.Change);				
			}
			
			foreach (var utxo in coinSelection.SelectedUtxos)
			{
		 		transactionBodyBuilder = transactionBodyBuilder.AddInput(utxo.TxHash, utxo.TxIndex);
		        _userWalletInputBlockchainAssets.Add(new UtxoAsset(utxo.TxHash.ThrowIfNull(), utxo.TxIndex, Enigmi.Constants.LovelaceTokenAssetId, utxo.Balance.Lovelaces));
			}
		}
		catch (Exception ex)
		{
			return ex.Message.ToFailedResponse<Enigmi.Constants.Unit>();
		}
		
		var witnessBuilder = CreateTransactionWitnessSetBuilder(mnemonic, policyClosingSlot);

		var transaction = BuildTransactionWithFee(transactionBodyBuilder, networkParams, witnessBuilder, auxDataBuilder);
		
		UnsignedTransactionCborHex = Convert.ToHexString(transaction.Serialize());
		TtlUtcTimestamp = latestSlotUtcTimestamp.AddSeconds(ttl);
		TransactionId = GetTransactionId(transaction);
		
		return new Enigmi.Constants.Unit().ToSuccessResponse();
	}

	private static IAuxiliaryDataBuilder BuildMetadataAndMintingTokens(List<OrderedPuzzlePiece> orderedPuzzlePieces,
		IEnumerable<PuzzlePieceMetadataList.PuzzlePieceMetadata> puzzlePieceMetadataList, ITokenBundleBuilder tokenBundleBuilder, ITokenBundleBuilder changeBundleBuilder)
	{
		var nftMetadata = CBORObject.NewMap();
		var policyIdString = string.Empty;
		foreach (var puzzlePiece in orderedPuzzlePieces)
		{
			(byte[] policyId, byte[] assetName) =
			CardanoHelper.ConvertAssetIdToPolicyIdAndAssetName(puzzlePiece.BlockchainAssetId);
			tokenBundleBuilder.AddToken(policyId, assetName, 1);
			changeBundleBuilder.AddToken(policyId, assetName, 1);
			var puzzlePieceMetadata = puzzlePieceMetadataList.Single(x => x.PuzzlePieceId == puzzlePiece.Id);
			var assetNameString = CardanoHelper.ConvertAssetNameToString(assetName);
			nftMetadata.Add(assetNameString, CBORObject.DecodeFromBytes(puzzlePieceMetadata.Metadata));
			policyIdString = Convert.ToHexString(policyId).ToLowerInvariant();
		}

		CBORObject metadata = CBORObject.NewMap();
		metadata.Add(policyIdString.ThrowIfNullOrWhitespace(), nftMetadata);

		var auxData = AuxiliaryDataBuilder.Create.AddMetadata(721, metadata);
		return auxData;
	}

	private static ITransactionWitnessSetBuilder CreateTransactionWitnessSetBuilder(string mnemonic, uint policyClosingSlot)
	{
		var policy = GetPolicyDetails(mnemonic, policyClosingSlot);

		var witnessBuilder = TransactionWitnessSetBuilder.Create;
		witnessBuilder.AddVKeyWitness(policy.PublicKey, policy.PrivateKey);
		witnessBuilder
			.AddVKeyWitness(policy.PublicKey, policy.PrivateKey)
			.SetScriptAllNativeScript(policy.PolicyScriptBuilder);
		return witnessBuilder;
	}

	private List<Utxo> ConvertToCardanoSharpUtxos(IEnumerable<UtxoAsset> userWalletAvailableAssets)
	{
		var utxos = userWalletAvailableAssets
			.GroupBy(x => (x.TxId, x.OutputIndexOnTx))
			.ToDictionary(x => x.Key, x => x.ToList());

		var allWalletUtxos = new List<Utxo>();
		foreach (var item in utxos)
		{
			var adaAmount = item.Value.Where(x => x.BlockchainAssetId == Enigmi.Constants.LovelaceTokenAssetId)
				.Select(x => x.Amount)
				.SingleOrDefault();

			var assets = item.Value.Where(x => x.BlockchainAssetId != Enigmi.Constants.LovelaceTokenAssetId).Select(x =>
				new Asset
				{
					Name = Convert.ToHexString(ConvertAssetIdToPolicyIdAndAssetName(x.BlockchainAssetId).AssetName),
					Quantity = (long)x.Amount,
					PolicyId = Convert.ToHexString(ConvertAssetIdToPolicyIdAndAssetName(x.BlockchainAssetId).PolicyId).ToLowerInvariant()
				}).ToList();

			allWalletUtxos.Add(new Utxo
			{
				Balance = new Balance
				{
					Assets = assets,
					Lovelaces = adaAmount
				},
				TxHash = item.Key.TxId,
				TxIndex = item.Key.OutputIndexOnTx
			});
		}

		return allWalletUtxos;
	}

	private static string GetTransactionId(Transaction transaction)
	{
		return Convert.ToHexString(HashUtility.Blake2b256(transaction.TransactionBody.GetCBOR(transaction.AuxiliaryData).EncodeToBytes()))
			.ToLowerInvariant();
	}

	private (byte[] PolicyId, byte[] AssetName) ConvertAssetIdToPolicyIdAndAssetName(string assetId)
	{
		assetId.IsNullOrWhitespace();
		
		var policyId = Convert.FromHexString(assetId.Substring(0, 56));
		var assetName = Convert.FromHexString(assetId.Substring(56));

		return (policyId, assetName);
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
		transactionBodyBuilder.RemoveFeeFromChange();

		transaction = transactionBuilder.Build();

		return transaction;
	}

    public bool CreateSignedTransaction(string cborWitness)
    {
        if (UnsignedTransactionCborHex == null)
        {
            return false;
        }

        cborWitness.ThrowIfNullOrWhitespace();
     
        var currentTx = UnsignedTransactionCborHex.HexToByteArray().DeserializeTransaction();
        var witness = cborWitness.HexToByteArray().DeserializeTransactionWitnessSet();
        
        if (currentTx.TransactionWitnessSet == null)
        {
	        currentTx.TransactionWitnessSet = new TransactionWitnessSet();
        }
        foreach (var vkeyWitness in witness.VKeyWitnesses)
        {
	        currentTx.TransactionWitnessSet.VKeyWitnesses.Add(vkeyWitness);
        }

        SignedTransactionCborHex = Convert.ToHexString(currentTx.Serialize());

        return true;
    }

    private static MintingPolicyDetails GetPolicyDetails(string mnemonicWords, long invalidAfter)
    {
	    if (invalidAfter > uint.MaxValue)
		    throw new ApplicationException($"Policy Invalid After {invalidAfter} cannot be casted to uint. The date is too far in the future.");

	    uint invalidAfterUint = (uint)invalidAfter;

	    var mnemonic = new MnemonicService().Restore(mnemonicWords);

	    var rootKey = mnemonic.GetRootKey();
	    var privateKey = rootKey.Derive("m/1852'/1815'/0'/0/0");
	    var publicKey = GetPublicKeyFixed(privateKey);
	    var publicKeyHash = HashUtility.Blake2b224(publicKey.Key);

	    var policySignScript = NativeScriptBuilder.Create
		    .SetKeyHash(publicKeyHash)
		    .SetInvalidAfter(invalidAfterUint);

	    var policyScriptBuilder = ScriptAllBuilder.Create
		    .SetScript(policySignScript);

	    var policyScript = policyScriptBuilder.Build();

	    return new MintingPolicyDetails()
	    {
		    PolicyId = policyScript.GetPolicyId(),
		    PolicyScript = policyScript,
		    PolicyScriptBuilder = policyScriptBuilder,
		    PrivateKey = privateKey,
		    PublicKey = publicKey,
		    InvalidHereAfter = invalidAfterUint,
	    };
    }

    public class MintingPolicyDetails
    {
	    public byte[] PolicyId { get; set; } = null!;

	    public ScriptAll PolicyScript { get; set; } = null!;

	    public IScriptAllBuilder PolicyScriptBuilder { get; set; } = null!;

	    public PrivateKey PrivateKey { get; set; } = null!;

	    public PublicKey PublicKey { get; set; } = null!;

	    public uint InvalidHereAfter { get; set; }
    }
    
    private static PublicKey GetPublicKeyFixed(PrivateKey privateKey)
    {
	    var pk = privateKey.GetPublicKey();
	    if (pk.Key.Length == 33)
	    {
		    pk = new PublicKey(pk.Key.Skip(1).ToArray(), pk.Chaincode);
	    }

	    return pk;
    }
}