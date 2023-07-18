using Enigmi.Application.ExtensionMethods;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.PuzzlePieceDispenser;
using Enigmi.Grains.Shared.PuzzlePieceDispenser.Messages;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace Enigmi.Application.Dispenser;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class PuzzlePieceDispenserGrain : GrainBase<Domain.Entities.PuzzlePieceDispenserAggregate.PuzzlePieceDispenser>, IPuzzlePieceDispenserGrain
{
    private readonly ILogger _logger;

    private IDisposable? ReleaseExpiredReservationsTimer { get; set; }

    public PuzzlePieceDispenserGrain(ILogger<PuzzlePieceDispenserGrain> logger)
    {
        _logger = logger.ThrowIfNull();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.DomainAggregate == null)
        {
            (Guid puzzleCollectionId, int puzzleSize) = PuzzlePieceDispenser.SplitId(this.GrainReference!.GrainId!.Key!.ToString()!);

            var grainSettingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(0);
            var grainSettings = await grainSettingsGrain.GetSettings();

            State.DomainAggregate = new PuzzlePieceDispenser(puzzleCollectionId, puzzleSize, grainSettings.PuzzlePieceDispenserGrain.DispenserExpiresTimespan);

            await WriteStateAsync();
        }

        var releaseInterval = TimeSpan.FromSeconds(10);
        ReleaseExpiredReservationsTimer = RegisterTimer(ReleaseExpiredReservationsHandler, this, releaseInterval, releaseInterval);

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<ResultOrError<Constants.Unit>> AddStock(IEnumerable<string> puzzlePieceIds)
    {
        puzzlePieceIds.ThrowIfNull();
        foreach (var puzzlePieceId in puzzlePieceIds)
        {
            State.DomainAggregate!.AddAvailablePuzzlePiece(puzzlePieceId);
        }

        await WriteStateAsync();
        return new Constants.Unit().ToSuccessResponse();
    }

    public async Task<ResultOrError<ReserveRandomPuzzlePiecesResponse>> ReserveRandomPuzzlePieces(ReserveRandomPuzzlePiecesCommand command)
    {
        command.ThrowIfNull();
        var reservedPuzzlePieceIds = State.DomainAggregate!.ReserveRandomPuzzlePieces(command.ReservationId, command.Quantity);
        await WriteStateAsync();

        return new ReserveRandomPuzzlePiecesResponse(reservedPuzzlePieceIds.ToList()).ToSuccessResponse();
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        @event.ThrowIfNull();
        this.State.DomainAggregate.ThrowIfNull();

        string subscriptionName = @event switch
        {
            _ => string.Empty,
        };

        return subscriptionName;
    }

    private async Task ReleaseExpiredReservationsHandler(object state)
    {
        await this.SelfInvokeAfter<IPuzzlePieceDispenserGrain>(o => o.ReleaseExpiredReservations());
    }

    public async Task ReleaseExpiredReservations()
    {
        if (State.DomainAggregate == null)
        {
            return;
        }

        State.DomainAggregate.ReleaseExpiredReservations();
        await WriteStateAsync();
    }

    public Task<bool> HasStockAvailable()
    {
        State.DomainAggregate.ThrowIfNull();
        return Task.FromResult(State.DomainAggregate.AvailablePuzzlePieceIds.Any());
    }

    public Task<PuzzlePieceDispenser> GetPuzzlePieceDispenser()
    {
        State.DomainAggregate.ThrowIfNull();
        return Task.FromResult(State.DomainAggregate);
    }

    public Task UpdateDispenserExpiresTimespan(TimeSpan dispenserExpiresTimespan)
    {
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.UpdateDispenserExpiresTimespan(dispenserExpiresTimespan);
        return Task.CompletedTask;
    }

    public Task<ResultOrError<Constants.Unit>> Release(Guid reservationId)
    {
        _logger.LogInformation(Invariant($"PuzzlePieceDispenserGrain: Release Order - {reservationId}"));
        reservationId.ThrowIfEmpty();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReleaseOrder(reservationId);
        return Task.FromResult(new Constants.Unit().ToSuccessResponse());
    }

    public async Task<ResultOrError<bool>> CommitReservation(Guid reservationId)
    {
        reservationId.ThrowIfEmpty();
        _logger.LogInformation(Invariant($"PuzzlePieceDispenserGrain: Commit Reservation - {reservationId}"));
        State.DomainAggregate.ThrowIfNull();
        var isSuccess = State.DomainAggregate.CommitReservation(reservationId.ThrowIfEmpty());
        if (!isSuccess)
        {
            return "Unfortunately your order reservation has timed out. Please place a new order.".ToFailedResponse<bool>();
        }
        await WriteStateAsync();
        return isSuccess.ToSuccessResponse();
    }
}