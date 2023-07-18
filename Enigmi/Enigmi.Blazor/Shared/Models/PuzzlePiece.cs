using Enigmi.Common;

namespace Enigmi.Blazor.Shared.Models;

public class PuzzlePiece
{
    public Guid Id { get; private set; }
    
    public Guid PuzzleDefinitionId { get; private set; }

    public string ImageUrl { get; private set; }

    public decimal TradeInValueInAda { get; private set; }

    public bool IsOwned { get; private set; }

    public int AvailablePuzzlePieceCount { get; private set; }

    public PuzzlePiece(Guid id, Guid puzzleId, string imageUrl, decimal tradeInValueInAda, bool isOwned, int availablePuzzlePieceCount)
    {
        Id = id.ThrowIfEmpty();
        PuzzleDefinitionId = puzzleId.ThrowIfEmpty();
        ImageUrl = imageUrl.ThrowIfNullOrWhitespace();
        TradeInValueInAda = tradeInValueInAda.ThrowIfNull();
        IsOwned = isOwned;
        AvailablePuzzlePieceCount = availablePuzzlePieceCount.ThrowIf(x => x < 0);
    }
    
    public void UpdateAvailablePuzzlePieceCount(int availablePuzzlePieceCount)
    {
        AvailablePuzzlePieceCount = availablePuzzlePieceCount;
    }
}