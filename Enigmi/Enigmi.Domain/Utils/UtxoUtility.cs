using CardanoSharp.Wallet.CIPs.CIP14.Extensions;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Utilities;
using Domain.ValueObjects;
using Enigmi.Common;
using Utxo = CardanoSharp.Wallet.Models.Utxo;
using static System.FormattableString;

namespace Enigmi.Domain.Utils;

public static class UtxoUtility
{
    public static List<Utxo> ConvertToCardanoSharpUtxos(IEnumerable<UtxoAsset> userWalletAvailableAssets)
    {
        userWalletAvailableAssets.ThrowIfNull();
        
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
    
    private static (byte[] PolicyId, byte[] AssetName) ConvertAssetIdToPolicyIdAndAssetName(string assetId)
    {
        assetId.IsNullOrWhitespace();
		
        var policyId = Convert.FromHexString(assetId.Substring(0, 56));
        var assetName = Convert.FromHexString(assetId.Substring(56));

        return (policyId, assetName);
    }
    
    public static string GetTransactionId(Transaction transaction)
    {
        transaction.ThrowIfNull();
        
        return Convert.ToHexString(HashUtility.Blake2b256(transaction.TransactionBody.GetCBOR(transaction.AuxiliaryData).EncodeToBytes()))
            .ToLowerInvariant();
    }
    
    public static string BuildUtxoSubscriptionName(string txId, uint index)
    {
        return Invariant($"{txId}-{index}");
    }
}