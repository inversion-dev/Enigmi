namespace Domain.ValueObjects;

public record struct UtxoAsset(string TxId, uint OutputIndexOnTx, string BlockchainAssetId, ulong Amount, string Fingerprint)
{
	public Utxo GetUtxo() => new Utxo(TxId, OutputIndexOnTx);
}