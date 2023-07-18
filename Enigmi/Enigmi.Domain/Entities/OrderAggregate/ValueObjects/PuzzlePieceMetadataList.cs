using Enigmi.Common;
using static System.FormattableString;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;

namespace Enigmi.Domain.Entities.OrderAggregate.ValueObjects;

public class PuzzlePieceMetadataList
{ 
    private List<PuzzlePieceMetadata> _puzzlePieceMetadata = new List<PuzzlePieceMetadata>();

    public IEnumerable<PuzzlePieceMetadata> List
    {
        get { return _puzzlePieceMetadata.AsReadOnly(); }
        private set { _puzzlePieceMetadata = value.ToList(); }
    }

    public void AddPuzzlePieceMetadata(string puzzlePieceId, byte[] metadata)
    {
        puzzlePieceId.ThrowIfNullOrWhitespace();
        metadata.ThrowIfNullOrEmpty();
        
        if (_puzzlePieceMetadata.Any(x => x.PuzzlePieceId == puzzlePieceId))
        {
            throw new ApplicationException(Invariant($"Metadata for puzzle piece {puzzlePieceId} has already been added"));
        }
        
        _puzzlePieceMetadata.Add(new PuzzlePieceMetadata(puzzlePieceId, metadata));
    }

    public record PuzzlePieceMetadata(string PuzzlePieceId, byte[] Metadata);
}