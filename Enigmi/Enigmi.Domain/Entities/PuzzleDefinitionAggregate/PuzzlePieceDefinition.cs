using Enigmi.Common;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.PuzzleDefinitionAggregate;

public class PuzzlePieceDefinition
{
    public PuzzlePieceDefinition(Guid id, int gridX, int gridY, string imageBlobPath)
    {  
        Id = id.ThrowIfEmpty();
        GridX = gridX.ThrowIf(x => x < 0, "grid value must be positive");
        GridY = gridY.ThrowIf(x => x < 0, "grid value must be positive"); ;
        ImageBlobPath = imageBlobPath.ThrowIfNullOrWhitespace();
    }
    
    [JsonProperty]
    public Guid Id { get; private set; }

    [JsonProperty]
    public int GridY { get; private set; }

    [JsonProperty]
    public int GridX { get; private set; }

    [JsonProperty]
    public string ImageBlobPath { get; private set; }

    [JsonProperty]
    public int NumberOfCopies { get; private set; }
}