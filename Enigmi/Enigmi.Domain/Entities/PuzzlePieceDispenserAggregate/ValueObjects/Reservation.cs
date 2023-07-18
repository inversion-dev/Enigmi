namespace Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate.ValueObjects;

public record Reservation(Guid Id, IEnumerable<string> PuzzlePieceIds, DateTime ReservedUtcDate)
{
    public ReservationState ReservationState { get; set; }
}