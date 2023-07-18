using static System.FormattableString;

namespace Domain.ValueObjects;

public record struct Utxo(string TxId, uint OutputIndexOnTx)
{
	public override string ToString() => Invariant($"({TxId}:{OutputIndexOnTx})");
}