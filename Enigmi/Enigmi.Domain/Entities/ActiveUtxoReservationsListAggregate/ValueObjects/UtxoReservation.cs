namespace Enigmi.Domain.Entities.ActiveUtxoReservationsListAggregate.ValueObjects;

public record UtxoReservation(string TxId, uint OutputIndexOnTx, IEnumerable<string> AssetFingerPrints);