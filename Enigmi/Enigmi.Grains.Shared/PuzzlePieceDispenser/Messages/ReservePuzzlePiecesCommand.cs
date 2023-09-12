namespace Enigmi.Grains.Shared.PuzzlePieceDispenser.Messages;

public record ReservePuzzlePiecesCommand(Guid ReservationId, int Quantity)
{
}

public record ReservePuzzlePiecesResponse(List<string> DispensedPuzzlePieceIds)
{
	public List<string> DispensedPuzzlePieceIds { get; set; } = DispensedPuzzlePieceIds ?? new List<string>();
}