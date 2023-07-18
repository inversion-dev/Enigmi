using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.PuzzleCollectionListAggregate.ValueObjects;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.PuzzleCollectionListAggregate;

public class PuzzleCollectionList : DomainEntity
{
    public PuzzleCollectionList(long id)
    {
        Id = Id.ThrowIf(x => x < 0);
    }

    [JsonProperty]
    public long Id { get; private set; }

    private List<PuzzleCollection> _puzzleCollections = new();
    
    [JsonProperty]
    public IEnumerable<PuzzleCollection> PuzzleCollections
    {
        get { return _puzzleCollections.AsReadOnly(); }
        private set { _puzzleCollections = value.ToList(); }
    }

    public void AddPuzzleCollection(PuzzleCollection puzzleCollection)
    {
        puzzleCollection.ThrowIfNull();
        _puzzleCollections.Add(puzzleCollection);
    }
}