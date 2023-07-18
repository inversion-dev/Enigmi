using Enigmi.Common;
using Enigmi.Common.Domain;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.PuzzleCollectionSnifferAggregate;

public class PuzzleCollectionSniffer : DomainEntity
{
    private List<PuzzleCollection> _puzzleCollections { get; set; } = new List<PuzzleCollection>();
    
    [JsonProperty]
    public IEnumerable<PuzzleCollection> PuzzleCollections
    {
        get { return _puzzleCollections.AsReadOnly(); }
        private set { _puzzleCollections = value.ToList(); }
    }

    public void CreatePuzzleCollection(Guid puzzleCollectionId, string sourceBlobFolderPath)
    {
        if (_puzzleCollections.Any(o => o.SourceBlobPath.InvariantIgnoreCaseEquals(sourceBlobFolderPath)))
        {
            throw new Exception($"Puzzle collection with source blob folder path {sourceBlobFolderPath} has already been added");
        }

        _puzzleCollections.Add(new(puzzleCollectionId, sourceBlobFolderPath));
    }

    public Guid? RemovePuzzleCollection(string sourceBlobFolderPath)
    {
        var toRemove = _puzzleCollections.SingleOrDefault(o => o.SourceBlobPath.InvariantIgnoreCaseEquals(sourceBlobFolderPath));
        if (toRemove != null)
        {
            _puzzleCollections.Remove(toRemove);
        }
        return toRemove?.Id;
    }
}