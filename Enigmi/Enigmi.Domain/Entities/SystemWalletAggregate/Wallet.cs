using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate.Events;
using Enigmi.Domain.ValueObjects;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.SystemWalletAggregate;

public abstract class Wallet : DomainEntity
{
    [JsonProperty]
    protected Dictionary<string,UtxoReservation> _utxoReservations = new();
	
    [JsonProperty]
    protected List<UtxoAsset> _availableUtxoAssets = new List<UtxoAsset>();

    public IReadOnlyDictionary<string,UtxoReservation> UtxoReservations => _utxoReservations.AsReadOnly();

    public IEnumerable<UtxoAsset> AvailableUtxoAssets
    {
        get { return _availableUtxoAssets.AsReadOnly(); }
    }
    
    public IEnumerable<UtxoAsset> UnreservedUtxoAssets
    {
        get { return _availableUtxoAssets.Where(x => !ReservedUtxos.Any(r => r.TxId == x.TxId && r.OutputIndexOnTx == x.OutputIndexOnTx)).ToList().AsReadOnly(); }
    }
    
    protected IEnumerable<Utxo> ReservedUtxos => UtxoReservations.Values.SelectMany(x => x.Utxos);

    public void ReserveUtxos(IEnumerable<Utxo> utxosToReserve, 
        IEnumerable<string> reservedAssetFingerprints,
        string reservedBy)
    {
        _utxoReservations.Remove(reservedBy);

        var reservationConflicts = ReservedUtxos.Intersect(utxosToReserve).ToList();
        if (reservationConflicts.Count > 0)
            throw new Exception(
                $"The following utxos have already been reserved: {string.Join(",", reservationConflicts.Select(o => o.ToString()))}");
        
        _utxoReservations.Add(reservedBy,new UtxoReservation(utxosToReserve, reservedAssetFingerprints));
        
        foreach (var utxo in utxosToReserve)
        {
            RaiseEvent(new UtxoReservationStateChanged(new Utxo(utxo.TxId,utxo.OutputIndexOnTx), ReservationState.Reserved, reservedBy, reservedAssetFingerprints));
        }
    }
    
    public bool DoesReservationExist(string reservedBy)
    {
        return _utxoReservations.ContainsKey(reservedBy);
    }

    public string GetReserveByKey(Reserver reserver, Guid reserverId)
    {
        return FormattableString.Invariant($"{reserver.ToString()}-{reserverId.ToString()}");
    }
    
    public void ReleaseUtxoReservations(string reservedBy)
    {
        reservedBy.ThrowIfNullOrEmpty();
        if (_utxoReservations.ContainsKey(reservedBy))
        {
            var reservation = _utxoReservations[reservedBy];
            foreach (var utxo in reservation.Utxos)
            {
                RaiseEvent(new UtxoReservationStateChanged(utxo, ReservationState.Released, reservedBy, reservation.AssetFingerPrints));
            }
        }
        _utxoReservations.Remove(reservedBy);
    }
}