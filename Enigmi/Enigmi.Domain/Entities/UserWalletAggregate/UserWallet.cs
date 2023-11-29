using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Domain.Entities.SystemWalletAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate.Events;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.UserWalletAggregate;

public class UserWallet : Wallet
{
	public UserWallet(string id)	
	{
		Id = id.ThrowIfNullOrWhitespace();
	}
		
	[JsonConstructor]
	private UserWallet()	
	{
		
	}

	[JsonProperty]
	public string Id { get; private set; } = null!;

	public string StakingAddress => Id;

	[JsonProperty]
    public string Nickname { get; private set; } = string.Empty;
    
    [JsonProperty]
    public string PaymentAddress { get; private set; } = string.Empty;

    [JsonProperty] 
	public OnlineState OnlineState { get; private set; } = OnlineState.Offline;
	
	[JsonProperty]
	public DateTime? LastRoundTripPingReceivedUtcTimestamp { get; private set; }
	
	[JsonProperty]
	public Guid ActiveOrderId { get; private set; }

	public void SetActiveOrder(Guid orderId, IEnumerable<Utxo> utxosUsed, IEnumerable<string> reservedFingerprints)
	{
		ReserveUtxos(utxosUsed,reservedFingerprints, this.GetReserveByKey(Reserver.Order, orderId));
		ActiveOrderId = orderId;
	}

	public void UpdateWalletState(IEnumerable<UtxoAsset> utxoAssets, string paymentAddress)
	{
		paymentAddress.ThrowIfNullOrWhitespace();
		_availableUtxoAssets = utxoAssets.ThrowIfNull().ToList();
		PaymentAddress = paymentAddress;
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
	
	public void SetNickname(string nickname)
	{
		this.Nickname = nickname.ThrowIfNullOrWhitespace();
	}
}