using Domain.ValueObjects;
using Enigmi.Common.Domain;

namespace Enigmi.Domain.Entities.UserWalletAggregate.Events;

public record UtxoReservationStateChanged(Utxo Utxo, ReservationState ReservationState, string ReserveBy,
    IEnumerable<string> ReservedAssetFingerprints): DomainEvent;