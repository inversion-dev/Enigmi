using Enigmi.Common;

namespace Enigmi.Blazor.Shared.Models;

public class Puzzle
{
    public Guid Id { get; private set; }

    public Guid PuzzleCollectionId { get; private set; }
    
    public string Title { get; private set; }

    public string? ImageUrl { get; set; }

    public int PuzzleSize { get; private set; }

    public int NumberOfAllowedBuilds { get; private set; }

    public int NumberOfCompletedBuilds { get; private set; }

    public Puzzle(Guid id, Guid puzzleCollectionId, string title, int puzzleSize, int numberOfAllowedBuilds, int numberOfCompletedBuilds)
    {
        Id = id.ThrowIfEmpty();
        PuzzleCollectionId = puzzleCollectionId.ThrowIfEmpty();
        Title = title.ThrowIfNullOrWhitespace();
        PuzzleSize = puzzleSize.ThrowIf(x => x <= 0, "Must be larger than 0");
        NumberOfAllowedBuilds = numberOfAllowedBuilds.ThrowIf(x => x < 0, "Must be equal or larger than 0");
        NumberOfCompletedBuilds = numberOfCompletedBuilds.ThrowIf(x => x < 0, "Must be equal or larger than 0");
    }

    public void SetImageUrl(string imageUrl)
    {
        ImageUrl = imageUrl.ThrowIfNullOrWhitespace();
    }
}
