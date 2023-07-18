namespace Enigmi.Grains.Shared.PuzzlePieceDispenser.Messages;

public record ReserveRandomPuzzlePiecesCommand(Guid ReservationId, int Quantity)
{
}

public record ReserveRandomPuzzlePiecesResponse(List<string> DispensedPuzzlePieceIds)
{
	public List<string> DispensedPuzzlePieceIds { get; set; } = DispensedPuzzlePieceIds ?? new List<string>();
}