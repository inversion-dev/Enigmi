using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate.Events;
using Enigmi.Grains.Shared.ActiveUtxoReservationsList;
using Orleans.Providers;
using UtxoReservation = Enigmi.Domain.Entities.ActiveUtxoReservationsListAggregate.ValueObjects.UtxoReservation;

namespace Enigmi.Application.ActiveUtxoReservationsList;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class ActiveUtxoReservationsListGrain  : GrainBase<Domain.Entities.ActiveUtxoReservationsListAggregate.ActiveUtxoReservationsList>, IActiveUtxoReservationsListGrain
{
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        State.DomainAggregate ??= new Domain.Entities.ActiveUtxoReservationsListAggregate.ActiveUtxoReservationsList(this.GetPrimaryKeyString());

        await Subscribe<UtxoReservationStateChanged>(State.DomainAggregate.Id, OnUtxoReservationStateChanged);
        await base.OnActivateAsync(cancellationToken);
    }

    private async Task OnUtxoReservationStateChanged(UtxoReservationStateChanged @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        
        var reservation = new UtxoReservation(@event.Utxo.TxId, @event.Utxo.OutputIndexOnTx, @event.ReservedAssetFingerprints);
        
        switch (@event.ReservationState)
        {
            case ReservationState.Reserved:
                State.DomainAggregate.AddReservation(reservation);
                break;
            case ReservationState.Released:
                State.DomainAggregate.RemoveReservation(reservation);
                break;
        }

        await WriteStateAsync();
    }

    public override IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event)
    {
        @event.ThrowIfNull();
        return new List<string>();
    }

    public Task Initialize()
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetReservedPuzzlePieces()
    {
        State.DomainAggregate.ThrowIfNull();
        IEnumerable<string> result = State.DomainAggregate.UtxoReservation.SelectMany(x => x.AssetFingerPrints).ToList()
            .AsReadOnly();
        
        return Task.FromResult(result);
    }
}