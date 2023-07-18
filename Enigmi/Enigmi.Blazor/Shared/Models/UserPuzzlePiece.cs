using Enigmi.Common;

namespace Enigmi.Blazor.Shared.Models;

public class UserPuzzlePiece
{ 
    public Guid PuzzlePieceDefinitionId { get; private set; }

    public PuzzlePiece? PuzzlePiece { get; private set; }

    public int OwnedCount { get; private set; } = 0;

    public int OfferCount { get; private set; } = 0;

    public UserPuzzlePiece(Guid puzzlePieceId)
    {        
        PuzzlePieceDefinitionId = puzzlePieceId.ThrowIfEmpty();
    }

    public void SetPuzzlePiece(PuzzlePiece puzzlePiece)
    {
        PuzzlePiece = puzzlePiece.ThrowIfNull();
    }

    public void SetOwnedCount(int ownedCount)
    {
        OwnedCount = ownedCount.ThrowIf(x => x < 0, "Must be equal to or larger than 0");
    }

    public void SetOfferCount(int offerCount)
    {
        OfferCount = offerCount.ThrowIf(x => x < 0, "Must be equal to or larger than 0");
    }
}
