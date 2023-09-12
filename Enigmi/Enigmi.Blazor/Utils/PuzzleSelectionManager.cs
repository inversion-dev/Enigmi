using Enigmi.Blazor.Shared.Models;
using Enigmi.Common;
using Enigmi.Messages.ActivePuzzlePieceList;

namespace Enigmi.Blazor.Utils;

public class PuzzleSelectionManager
{
    private ApiClient ApiClient { get; }

    public PuzzleSelectionManager(ApiClient apiClient)
    {
        ApiClient = apiClient;
    }

    private Guid? _selectedPuzzleDefinitionDefinitionId;

    public int Index { get; set; } = 0;

    public Guid? SelectedPuzzleDefinitionId
    {
        get => _selectedPuzzleDefinitionDefinitionId;
        private set
        {
            if (_selectedPuzzleDefinitionDefinitionId == value)
            {
                return;
            }
            
            _selectedPuzzleDefinitionDefinitionId = value;
            OnPuzzleSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public List<UserPuzzle>? Puzzles { get; private set; }

    private List<UserPuzzle> PlatformUserPuzzles { get; set; } = new List<UserPuzzle>();

    public List<UserPuzzle> AllPuzzles
    {
        get
        {
            var puzzles = new List<UserPuzzle>();
            puzzles.AddRange(Puzzles ?? new List<UserPuzzle>());
            var missingPuzzles = PlatformUserPuzzles.Except(Puzzles ?? new List<UserPuzzle>(), new UserPuzzleComparer()).ToList();
            puzzles.AddRange(missingPuzzles);
            return puzzles;
        }
    }

    public event EventHandler? OnPuzzleSelectionChanged;

    public void UpdateUserPuzzles(List<UserPuzzle> userPuzzles)
    {
        Puzzles = userPuzzles.ThrowIfNull();

        if (SelectedPuzzleDefinitionId.HasValue)
        {
            Index = Puzzles.FindIndex(x => x.PuzzleId == SelectedPuzzleDefinitionId);
            if (Index >= 0)
            {
                return;
            }

            _selectedPuzzleDefinitionDefinitionId = null;
        }
        
        if (!SelectedPuzzleDefinitionId.HasValue && userPuzzles.Any())
        {
            Index = 0;
            SelectedPuzzleDefinitionId = Puzzles[Index].PuzzleId;
        }
    }

    public void Next()
    {
        if (Puzzles == null)
        {
            return;
        }
        
        if (Index >= (Puzzles.Count - 1))
        {
            return;
        }

        Index++;
        SelectedPuzzleDefinitionId = Puzzles[Index].PuzzleId;
    }

    public void Previous()
    {
        if (Puzzles == null)
        {
            return;
        }
        
        if (Index == 0)
        {
            return;
        }

        Index--;
        SelectedPuzzleDefinitionId = Puzzles[Index].PuzzleId;
    }

    public void SetSelectedPuzzleDefinitionId(Guid puzzleDefinitionId)
    {
        puzzleDefinitionId.ThrowIfEmpty();

        if (Puzzles == null)
        {
            return;
        }

        Index = Puzzles.FindIndex(x => x.PuzzleId == puzzleDefinitionId);
        if (Index < 0)
        {
            return;
        }
        
        this.SelectedPuzzleDefinitionId = Puzzles[Index].PuzzleId;
    }

    public async Task EnsurePuzzleDefinitionsAreLoaded(IEnumerable<Guid> requiredPuzzleDefinitionIds)
    {        
        var loadedPuzzleDefinitionIds = this.AllPuzzles.Select(x => x.PuzzleId).ToList();
        var missingPuzzleDefinitionIds = requiredPuzzleDefinitionIds.ToList().Except(loadedPuzzleDefinitionIds).ToList();
     
        if (!missingPuzzleDefinitionIds.Any())
        {
            return;
        }

        var puzzleDefinitionListResponse = await this.ApiClient.SendAsync(new GetPuzzleDefinitionsRequest(missingPuzzleDefinitionIds));
        if (puzzleDefinitionListResponse != null)
        {
            var puzzles = puzzleDefinitionListResponse.PuzzleDefinitions.Select(x => new UserPuzzle(
                x.Id,
                x.Id,
                x.Title,
                x.PuzzleCollectionTitle,
                x.NumberOfAllowedBuilds,
                x.NumberOfCompletedBuilds,
                x.PuzzleSize,
                x.PuzzlePieceDefinitions.Select(ConvertToUserPuzzlePiece).ToList()       
            ));

            PlatformUserPuzzles.AddRange(puzzles);
        }
    }

    private static UserPuzzlePiece ConvertToUserPuzzlePiece(GetPuzzleDefinitionsResponse.PuzzlePieceDefinition puzzlePieceDefinition)
    {
        var puzzlePiece = new UserPuzzlePiece(puzzlePieceDefinition.Id);
        puzzlePiece.SetPuzzlePiece(new PuzzlePiece(
            puzzlePieceDefinition.Id, 
            puzzlePieceDefinition.PuzzleDefinitionId,
            puzzlePieceDefinition.ImageUrl,
            0,
            false,
            0));
        
        return puzzlePiece;
    }
}
