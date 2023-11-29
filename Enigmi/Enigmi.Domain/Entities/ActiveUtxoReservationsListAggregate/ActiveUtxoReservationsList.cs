using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.ActiveUtxoReservationsListAggregate.ValueObjects;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.ActiveUtxoReservationsListAggregate;

public class ActiveUtxoReservationsList : DomainEntity
{
    public ActiveUtxoReservationsList(string id)
    {
        Id = id;
    }
    
    [JsonProperty]
    public string Id { get; set; }
    
    
    private List<UtxoReservation> _utxoReservations = new();

    [JsonProperty]
    public IEnumerable<UtxoReservation> UtxoReservation
    {
        get => _utxoReservations.AsReadOnly();
        set => _utxoReservations = value.ToList();
    }
    
    public void AddReservation(UtxoReservation reservation)
    {
        reservation.ThrowIfNull();
        _utxoReservations.Add(reservation);
    }
    
    public void RemoveReservation(UtxoReservation reservation)
    {
        reservation.ThrowIfNull();
        _utxoReservations.RemoveAll(x => x.TxId == reservation.TxId && x.OutputIndexOnTx == reservation.OutputIndexOnTx);
    }
}