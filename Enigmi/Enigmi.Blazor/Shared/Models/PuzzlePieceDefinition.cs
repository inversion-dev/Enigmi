namespace Enigmi.Blazor.Shared.Models;

public class PuzzlePieceDefinition
{
    public Guid Id { get; set; }

    public Guid PuzzleDefinitionId { get; set; }

    public DateTime UtcTimestamp { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int PuzzlePieceCount { get; set; }
}