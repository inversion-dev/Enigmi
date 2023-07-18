using Enigmi.Blazor.Shared.Models;
using Enigmi.Common;

namespace Enigmi.Blazor.Utils;

public class PuzzleSelectionManager
{
    private Guid? _selectedPuzzleDefinitionDefinitionId;

    private int _index = 0;

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

    private List<UserPuzzle>? Puzzles { get; set; }

    public event EventHandler? OnPuzzleSelectionChanged;

    public void UpdateUserPuzzles(List<UserPuzzle> userPuzzles)
    {
        Puzzles = userPuzzles.ThrowIfNull();

        if (SelectedPuzzleDefinitionId.HasValue)
        {
            _index = Puzzles.FindIndex(x => x.PuzzleId == SelectedPuzzleDefinitionId);
            if (_index >= 0)
            {
                return;
            }

            _selectedPuzzleDefinitionDefinitionId = null;
        }
        
        if (!SelectedPuzzleDefinitionId.HasValue && userPuzzles.Any())
        {
            _index = 0;
            SelectedPuzzleDefinitionId = Puzzles[_index].PuzzleId;
        }
    }

    public void Next()
    {
        if (Puzzles == null)
        {
            return;
        }
        
        if (_index >= (Puzzles.Count - 1))
        {
            return;
        }

        _index++;
        SelectedPuzzleDefinitionId = Puzzles[_index].PuzzleId;
    }

    public void Previous()
    {
        if (Puzzles == null)
        {
            return;
        }
        
        if (_index == 0)
        {
            return;
        }

        _index--;
        SelectedPuzzleDefinitionId = Puzzles[_index].PuzzleId;
    }

    public void SetSelectedPuzzleDefinitionId(Guid puzzleDefinitionId)
    {
        puzzleDefinitionId.ThrowIfEmpty();

        if (Puzzles == null)
        {
            return;
        }
        
        _index = Puzzles.FindIndex(x => x.PuzzleId == puzzleDefinitionId);
        if (_index < 0)
        {
            return;
        }
        
        this.SelectedPuzzleDefinitionId = Puzzles[_index].PuzzleId;
    }
}
