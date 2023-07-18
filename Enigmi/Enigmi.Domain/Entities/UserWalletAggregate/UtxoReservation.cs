using Domain.ValueObjects;

namespace Enigmi.Domain.Entities.UserWalletAggregate;

public class UtxoReservation
{
	public Guid Id { get; private set; } = Guid.NewGuid();

	public Utxo Utxo { get; private set; }

	public Reserver Reserver { get; private set; }

	public Guid ReserverId { get; private set; }

	public UtxoReservation(Utxo utxo, Reserver reserver, Guid reserverId)
	{
		Utxo=utxo;
		Reserver=reserver;
		ReserverId=reserverId;
	}
}

