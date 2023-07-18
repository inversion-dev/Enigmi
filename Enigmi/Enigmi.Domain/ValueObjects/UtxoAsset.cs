namespace Domain.ValueObjects;

public record struct UtxoAsset(string TxId, uint OutputIndexOnTx, string BlockchainAssetId, ulong Amount)
{
	public Utxo GetUtxo() => new Utxo(TxId, OutputIndexOnTx);
}