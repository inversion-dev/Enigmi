using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.UserWalletAggregate.Events;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.UserWalletAggregate;

public class UserWallet : DomainEntity
{
	public Guid Id { get; private set; }
	
	public int? WalletHash { get; private set; }

	[JsonProperty] 
	public OnlineState OnlineState { get; private set; } = OnlineState.Offline;
	
	[JsonProperty]
	public DateTime? LastRoundTripPingReceivedUtcTimestamp { get; private set; }
	
	[JsonProperty]
	public Guid ActiveOrderId { get; private set; }

	private List<UtxoReservation> _utxoReservations = new List<UtxoReservation>();
	
	private List<UtxoAsset> _availableUtxoAssets = new List<UtxoAsset>();

    [JsonProperty]
    public IEnumerable<UtxoReservation> UtxoReservations
	{
		get { return _utxoReservations.AsReadOnly(); }
		private set { _utxoReservations = value.ToList(); }
	}

    [JsonProperty]
    public IEnumerable<UtxoAsset> AvailableUtxoAssets
	{
		get { return _availableUtxoAssets.AsReadOnly(); }
		private set { _availableUtxoAssets = value.ToList(); }
	}
    
	public IEnumerable<Utxo> ReservedUtxos => UtxoReservations.Select(o => o.Utxo);

	public void SetActiveOrder(Guid orderId, IEnumerable<Utxo> utxosUsed)
	{
		_utxoReservations.RemoveAll(o => o.Reserver == Reserver.Order && o.ReserverId == ActiveOrderId);

		var reservationConflicts = ReservedUtxos.Intersect(utxosUsed).ToList();
		if (reservationConflicts.Count > 0)
			throw new Exception($"The following utxos have already been reserved: {string.Join(",", reservationConflicts.Select(o => o.ToString()))}");

		_utxoReservations.AddRange(utxosUsed.Select(o => new UtxoReservation(o, Reserver.Order, orderId)));
		
		ActiveOrderId = orderId;
	}

	public void UpdateWalletState(IEnumerable<UtxoAsset> utxoAssets)
	{
		_availableUtxoAssets = utxoAssets.ThrowIfNull().ToList();
	}

	public void GoOnline()
	{
		OnlineState = OnlineState.Online;
		KeepAlive();
	}
	
	public void KeepAlive()
	{
		LastRoundTripPingReceivedUtcTimestamp = DateTime.UtcNow;
	}

	public void GoOffline()
	{
		if (OnlineState == OnlineState.Offline) return;
		
		OnlineState = OnlineState.Offline;
		RaiseEvent(new UserWalletWentOffline());
	}

	public void OnCancelOrder(Guid orderId)
	{
		var reservation = _utxoReservations.FirstOrDefault(x => x.ReserverId == orderId);
		if (reservation != null)
		{
			_utxoReservations.Remove(reservation);
		}
	}	
}