using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.PuzzleDefinitionAggregate.Events;
using Newtonsoft.Json;
using static System.FormattableString;

namespace Enigmi.Domain.Entities.PuzzleDefinitionAggregate;

public class PuzzleDefinition : DomainEntity
{
    public PuzzleDefinition(Guid id, Guid puzzleCollectionId, string title, int puzzleSize, int numberOfPuzzlePiecesTarget)
    {
        Id = id.ThrowIfEmpty();
        PuzzleCollectionId = puzzleCollectionId.ThrowIfEmpty();
        Title =  title.ThrowIfNullOrWhitespace();
        PuzzleSize =   puzzleSize.ThrowIf(x => x <= 0, "Puzzle size should be larger than 0");
        NumberOfPuzzlePiecesTarget = numberOfPuzzlePiecesTarget.ThrowIf(o => o < 1, "Target must be bigger than 0");
        CreatePuzzlePieceDefinitions();
    }

    [JsonConstructor]
    private PuzzleDefinition()
    {

    }

    private List<PuzzlePieceDefinition> _puzzlePieceDefinitions = new List<PuzzlePieceDefinition>();
    private List<string> _puzzlePieceIds = new List<string>();

    [JsonProperty]
    public Guid Id { get; private set; }
    
    [JsonProperty]
    public string Title { get; private set; } = null!;
    
    [JsonProperty]
    public Guid PuzzleCollectionId { get; private set; }

    [JsonProperty]
    public int PuzzleSize { get; private set; }

    [JsonProperty]
    public int NumberOfPuzzlePiecesTarget { get; private set; }
    
    [JsonProperty]
    public Enums.PublicationState State { get; private set; } = Enums.PublicationState.Unpublished;
    
    [JsonProperty]
    public int NumberOfAllowedBuilds { get; private set; } = 1;
    
    [JsonProperty]
    public int NumberOfCompletedBuilds { get; private set; }
    
    [JsonProperty]
    public string? BlockchainMetadataJson { get; private set; }

    [JsonProperty]
    public string? OriginalImageBlobPath { get; private set; }

    [JsonProperty]
    public ResizedBlobImage? NormalizedImage { get; private set; }

    [JsonProperty]
    public ResizedBlobImage? Thumbnail { get; private set; }

    [JsonProperty]
    public IEnumerable<PuzzlePieceDefinition> PuzzlePieceDefinitions
    {
        get { return _puzzlePieceDefinitions.AsReadOnly(); }
        private set { _puzzlePieceDefinitions = value.ToList(); }
    }

    [JsonProperty]
    public IEnumerable<string> PuzzlePieceIds
    {
        get { return _puzzlePieceIds.AsReadOnly(); }
        private set { _puzzlePieceIds = value.ToList(); }
    }

    public void AddPuzzlePieceId(string puzzlePieceId)
    {
        puzzlePieceId.ThrowIfNullOrWhitespace();
        
        if (_puzzlePieceIds.Any(x => x == puzzlePieceId))
        {
            throw new ApplicationException($"Puzzle id {puzzlePieceId} already added to collection");
        }
        _puzzlePieceIds.Add(puzzlePieceId);
    }

    public void SetBlockchainMetadataJson(string blockchainMetadataJson)
    {
        blockchainMetadataJson.ThrowIfNullOrEmpty();

        BlockchainMetadataJson = blockchainMetadataJson;
    }

    public void SetNumberOfAllowedBuilds(int numberOfAllowedBuilds)
    {
        numberOfAllowedBuilds.ThrowIf(x => x <= 0, "Puzzle size should be larger than 0");
        numberOfAllowedBuilds.ThrowIf(x => x > NumberOfPuzzlePiecesTarget, "Number of allowed builds cannot be bigger than the amount of puzzle piece copies");
        this.NumberOfAllowedBuilds = numberOfAllowedBuilds;
    }

    public void SetOriginalImageBlobPath(string blobPath)
    {
        blobPath.ThrowIfNullOrEmpty();
        this.OriginalImageBlobPath = blobPath;
    }

    public void SetThumbnail(ResizedBlobImage image)
    {
        this.Thumbnail = image;
    }

    public void SetNormalizedImage(ResizedBlobImage image)
    {
        this.NormalizedImage = image;
    }

    public string RootBlobPath => Invariant($"/domain/PuzzleCollection/{PuzzleCollectionId}/PuzzleDefinition/{Id}");

    public string GetThumbnailBlobPath() => Invariant($"{RootBlobPath}/thumbnail.jpg");

    public string GetOriginalBlobPath() => Invariant($"{RootBlobPath}/original.jpg");

    public string GetNormalizedBlobPath() => Invariant($"{RootBlobPath}/normalized.jpg");

    public string GetPuzzleDefinitionBlobPath(Guid puzzlePieceDefinitionId) => Invariant($"{RootBlobPath}/PuzzlePieceDefinition/{puzzlePieceDefinitionId}/img.jpg");

    private void CreatePuzzlePieceDefinitions()
    {
        var columns = Math.Sqrt(PuzzleSize);

        for (var x = 0; x < columns; x++)
        {
            for (var y = 0; y < columns; y++)
            {
                var puzzlePieceDefinitionId = Guid.NewGuid();
                var imagePuzzlePieceBlobPath = GetPuzzleDefinitionBlobPath(puzzlePieceDefinitionId);
                var puzzlePieceDefinition = new PuzzlePieceDefinition(puzzlePieceDefinitionId, x, y, imagePuzzlePieceBlobPath);
                _puzzlePieceDefinitions.Add(puzzlePieceDefinition);
            }
        }
    }

    public (Point start, Dimension dimensions) GetPuzzlePieceDefinitionPosition(PuzzlePieceDefinition puzzlePieceDefinition)
    {
        puzzlePieceDefinition.ThrowIfNull();

        var imageDimension = this.NormalizedImage!.Value.Dimension;

        var tileWidth = imageDimension.Width / ColumnRowCount;
        var tileHeight = imageDimension.Height / ColumnRowCount;

        var x = puzzlePieceDefinition.GridX * tileWidth;
        var y = puzzlePieceDefinition.GridY * tileHeight;

        return (new(x, y), new(tileWidth, tileHeight));
    }

    public int ColumnRowCount => Convert.ToInt32(Math.Sqrt(this.PuzzleSize));

    public void MarkAsPublished()
    {
        OriginalImageBlobPath.ThrowIfNullOrEmpty();

        if (!_puzzlePieceDefinitions.Any())
        {
            throw new ApplicationException("Definition can not be marked as published when no puzzle piece definition exists");
        }
        
        State = Enums.PublicationState.Published;
        RaiseEvent(new PuzzleDefinitionPublished(Id));
    }

    
}