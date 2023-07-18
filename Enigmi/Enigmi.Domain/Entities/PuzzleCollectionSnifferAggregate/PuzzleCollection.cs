using Enigmi.Common;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.PuzzleCollectionSnifferAggregate;

public class PuzzleCollection
{
    [JsonConstructor]
    private PuzzleCollection()
    {
        
    }
    
    [JsonProperty]
    public Guid Id { get; private set; }

    [JsonProperty] 
    public string SourceBlobPath { get; private set; } = null!;

    public PuzzleCollection(Guid id, string sourceBlobPath)
    {
        Id = id.ThrowIfEmpty();
        SourceBlobPath = sourceBlobPath.ThrowIfNullOrWhitespace();
    }
}