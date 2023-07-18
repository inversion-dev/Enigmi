using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.PuzzleCollectionAggregate.Events;
using Enigmi.Domain.Entities.PuzzleCollectionAggregate.ValueObject;
using Enigmi.Domain.ValueObjects;
using Newtonsoft.Json;
using static System.FormattableString;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;

namespace Enigmi.Domain.Entities.PuzzleCollectionAggregate;

public class PuzzleCollection : DomainEntity
{
    public PuzzleCollection(Guid id, string title, string description, DateTime puzzleClosingUtcDate, DateTime puzzlePieceClosingUtcDate)
    {
        Id = id.ThrowIfEmpty();
        Title = title.ThrowIfNullOrWhitespace();
        Description = description.ThrowIfNullOrWhitespace();
        PuzzlePolicy = new Policy(puzzleClosingUtcDate.ThrowIfNull(), null);
        PuzzlePiecePolicy = new Policy(puzzlePieceClosingUtcDate.ThrowIfNull(), null);
        
        RaiseEvent(new PuzzleCollectionCreated(Id, PuzzlePolicy.ClosingUtcDate, PuzzlePiecePolicy.ClosingUtcDate));
    }
    
    [JsonConstructor]
    private PuzzleCollection()
    {
        
    }
    
    [JsonProperty]
    public Guid Id { get; private set; }

    [JsonProperty] 
    public Policy PuzzlePiecePolicy { get; private set; } = null!;
    
    [JsonProperty]
    public Policy PuzzlePolicy { get; private set; } = null!;

    private List<int> _permittedPuzzleSize { get; set; } = new List<int>();

    private List<PuzzleDefinition> _puzzleDefinitions { get; set; } = new List<PuzzleDefinition>();
    
    [JsonProperty]
    public IEnumerable<int> PermittedPuzzleSize
    {
        get { return _permittedPuzzleSize.AsReadOnly(); }
        private set { _permittedPuzzleSize = value.ToList(); }
    }
    
    [JsonProperty]
    public IEnumerable<PuzzleDefinition> PuzzleDefinitions
    {
        get { return _puzzleDefinitions.AsReadOnly(); }
        private set { _puzzleDefinitions = value.ToList(); }
    }
    
    [JsonProperty]
    public string Title { get; private set; } = null!;
    
    [JsonProperty]
    public ActivationStatus Status { get; private set; } = ActivationStatus.Draft;

    [JsonProperty]
    public string Description { get; private set; } = null!;
    
    [JsonProperty]
    public string? CoverImageBlobPath { get; private set; }
    
    [JsonProperty]
    public string? SourceBlobFolderPath { get; private set; }

    public ulong PuzzlePiecePriceInLovelace => Convert.ToUInt64(PuzzlePiecePriceInAda * Enigmi.Constants.LovelacePerAda);
    public ulong PuzzlePieceTradeInValueInLovelace => Convert.ToUInt64(PuzzlePieceTradeInValueInAda * Enigmi.Constants.LovelacePerAda);
    
    [JsonProperty]
    public decimal PuzzlePiecePriceInAda { get; private set; }
        
    [JsonProperty]
    public decimal PuzzlePieceTradeInValueInAda { get; private set; }

    public string GetPuzzleDefinitionImageSourceBlobPath(string puzzleName) => Invariant($"{SourceBlobFolderPath}/{puzzleName}.jpg");

    public string GetPuzzleDefinitionConfigSourceBlobPath(string puzzleName) => Invariant($"{SourceBlobFolderPath}/{puzzleName}.json");

    public string RootBlobPath => Invariant($"Domain/PuzzleCollection/{Id}");

    public void AddPuzzleDefinition(Guid puzzleDefinitionId)
    {
        puzzleDefinitionId.ThrowIfEmpty();
        _puzzleDefinitions.Add(new PuzzleDefinition(puzzleDefinitionId){ State = ActivationStatus.Draft });
    }

    public void SetCoverImageBlobPath(string coverImageBlobPath)
    {
        this.CoverImageBlobPath = coverImageBlobPath;
    }

    public void SetSourceBlobFolderPath(string sourceBlobFolderPath)
    {
        this.SourceBlobFolderPath = sourceBlobFolderPath;
    }

    public void SetPuzzlePiecePriceInAda(decimal price)
    {
        price.ThrowIf(x => x <= 0, "Puzzle piece prize must be a positive value");
        this.PuzzlePiecePriceInAda = price;
    }
    
    public void SetPuzzlePieceTradeInValueInAda(decimal price)
    {
        price.ThrowIf(x => x <= 0, "Puzzle piece prize must be a positive value");
        this.PuzzlePieceTradeInValueInAda = price;
    }

    public void AddPermittedPuzzleSize(int size)
    {
        if (!_permittedPuzzleSize.Contains(size))
        {
            _permittedPuzzleSize.Add(size);    
        }
    }

    public void SetPuzzlePolicy(Policy policy)
    {
        policy.ThrowIfNull();
        PuzzlePolicy = policy;
    }

    public void SetPuzzlePiecePolicy(Policy policy)
    {
        policy.ThrowIfNull();
        PuzzlePiecePolicy = policy;
        RaiseEvent(new PuzzlePiecePolicyAdded());
    }

    public bool CanBeMarkedAsPublished => PuzzlePolicy.PolicyId != null 
                                           && PuzzlePiecePolicy.PolicyId != null 
                                           && Status == ActivationStatus.Draft
                                           && PuzzleDefinitions.Any()
                                           && PuzzleDefinitions.All(x => x.State == ActivationStatus.Published);

    public void MarkedAsPublished()
    {
        if (!CanBeMarkedAsPublished)
        {
            throw new ApplicationException("Puzzle collection have not met the conditions to be marked as published");
        }

        Status = ActivationStatus.Published;
        RaiseEvent(new PuzzleCollectionPublished(Id));
    }

    public void MarkAsPublished(Guid puzzleDefinitionId)
    {
        var puzzleDefinition = _puzzleDefinitions.Single(x => x.Id == puzzleDefinitionId);
        puzzleDefinition.State = ActivationStatus.Published;
    }
}