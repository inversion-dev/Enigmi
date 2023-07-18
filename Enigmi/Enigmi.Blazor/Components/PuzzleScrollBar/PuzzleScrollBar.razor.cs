using Enigmi.Blazor.Events;
using Enigmi.Blazor.Shared.Models;
using Enigmi.Blazor.Utils;
using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Components.PuzzleScrollBar;

public partial class PuzzleScrollBar : IDisposable
{
    [Parameter]
    public List<UserPuzzle> UserPuzzles { get; set; } = new();

    [Inject]
    PuzzleSelectionManager PuzzleSelectionManager { get; set; } = null!;

    [Inject]
    private ActivePuzzlePieceUpdatedEvent ActivePuzzlePieceUpdatedEvent { get; set; } = null!;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            PuzzleSelectionManager.OnPuzzleSelectionChanged += RefreshState;
            ActivePuzzlePieceUpdatedEvent.Subscribe(RefreshState);
        }

        base.OnAfterRender(firstRender);
    }

    private void RefreshState(object? sender, EventArgs e)
    {
        StateHasChanged();
    }

    private void OnUserPuzzleClicked(UserPuzzle puzzle)
    {
        PuzzleSelectionManager.SetSelectedPuzzleDefinitionId(puzzle.PuzzleId);
    }

    public bool IsActivePuzzleDefinition(Guid puzzleDefinitionId) => PuzzleSelectionManager.SelectedPuzzleDefinitionId == puzzleDefinitionId;

    public void Dispose()
    {
        PuzzleSelectionManager.OnPuzzleSelectionChanged -= RefreshState;
        ActivePuzzlePieceUpdatedEvent.UnSubscribe(RefreshState);
    }
}
