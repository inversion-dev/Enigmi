using Domain.ValueObjects;

namespace Enigmi.Domain.Entities.UserWalletAggregate;

public class UtxoReservation
{
	public Guid Id { get; private set; } = Guid.NewGuid();
	
	public IEnumerable<Utxo> Utxos { get; private set; } = new List<Utxo>();
	
	public IEnumerable<string> AssetFingerPrints { get; private set; } = new List<string>();

	public UtxoReservation(IEnumerable<Utxo> utxos, IEnumerable<string> assetFingerPrints)
	{
		Utxos = utxos;
		AssetFingerPrints = assetFingerPrints;
	}
}

