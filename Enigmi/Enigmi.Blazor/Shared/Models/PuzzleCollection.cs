using System.Collections.ObjectModel;
using Enigmi.Common;

namespace Enigmi.Blazor.Shared.Models;

public class PuzzleCollection
{
    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public decimal PuzzlePiecePriceInAda { get; private set; }

    public ReadOnlyCollection<int> PermittedPuzzleSize { get; private set; }

    public PuzzleCollection(Guid id, string title, decimal puzzlePiecePriceInAda, IEnumerable<int> permittedPuzzleSize)
    {
        Id = id.ThrowIfEmpty();
        Title = title.ThrowIfNullOrWhitespace();
        PuzzlePiecePriceInAda = puzzlePiecePriceInAda.ThrowIfNull();
        PermittedPuzzleSize = permittedPuzzleSize.ToList().AsReadOnly();
    }
}
