using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate;

public class ActivePuzzlePieceList : DomainEntity
{
    private List<UserWalletPuzzlePiece> _activePuzzlePieces = new();
    
    [JsonProperty]
    public IEnumerable<UserWalletPuzzlePiece> ActivePuzzlePieces
    {
        get { return _activePuzzlePieces.AsReadOnly(); }
        private set { _activePuzzlePieces = value.ToList(); }
    }
    
    [JsonProperty]
    private Dictionary<Guid, PuzzleDefinition> _puzzleDefinitionDataDictionary = new();

    [JsonIgnore]
    public ReadOnlyDictionary<Guid, PuzzleDefinition> PuzzleDefinitionDataDictionary
    {
        get { return _puzzleDefinitionDataDictionary.AsReadOnly(); }
        private set { _puzzleDefinitionDataDictionary = value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); }
    }

    public void AddPuzzlePieces(List<UserWalletPuzzlePiece> puzzlePieces)
    {
        puzzlePieces.ThrowIfNull();
        _activePuzzlePieces.AddRange(puzzlePieces);
    }
    
    public void RemovePuzzlePieces(List<UserWalletPuzzlePiece> puzzlePieces)
    {
        puzzlePieces.ThrowIfNull();
        _activePuzzlePieces.RemoveAll(x => puzzlePieces.Any(y => y.PuzzlePieceId == x.PuzzlePieceId));
    }

    public void AddPuzzleDefinition(Guid puzzleDefinitionId, PuzzleDefinition puzzleDefinition)
    {
        puzzleDefinitionId.ThrowIfEmpty();
        puzzleDefinition.ThrowIfNull();
        _puzzleDefinitionDataDictionary.Add(puzzleDefinitionId, puzzleDefinition);
    }
}