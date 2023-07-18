using Enigmi.Blazor.Events;
using Enigmi.Blazor.Shared.Models;
using Enigmi.Blazor.Utils;
using Enigmi.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Enigmi.Blazor.Components.Puzzle;

public partial class Puzzle: IDisposable
{
    [Parameter]
    public bool Visible { get; set; }

    [Parameter]
    public List<UserPuzzle> UserPuzzles { get; set; } = new();    

    [Inject]
    private PuzzleSelectionManager PuzzleSelectionManager { get; set; } = null!;

    [Inject]
    private ActivePuzzlePieceUpdatedEvent ActivePuzzlePieceUpdatedEvent { get; set; } = null!;

    private Guid? SelectedPuzzlePieceId { get; set; }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            PuzzleSelectionManager.OnPuzzleSelectionChanged += RefreshState;
            ActivePuzzlePieceUpdatedEvent.Subscribe(RefreshState);
        }
        
        return base.OnAfterRenderAsync(firstRender);
    }

    private void RefreshState(object? sender, EventArgs e)
    {
        StateHasChanged();
    }

    private async Task AnimateFadeIn(IEnumerable<Guid> puzzlePieceIds)
    {
        // TODO: puzzle piece animations
        await Task.CompletedTask.ContinueOnAnyContext();
    }

    private async Task AnimateFadeOut(IEnumerable<Guid> puzzlePieceIds)
    {
        // TODO: puzzle piece animations
        await Task.CompletedTask.ContinueOnAnyContext();
    }

    private async Task BuildPuzzle(Guid puzzleId)
    {
        // TODO: implement
        await Task.CompletedTask.ContinueOnAnyContext();
    }

    private void MouseWheelEventHandler(WheelEventArgs e)
    {
        if (e.DeltaY > 0)
        {            
         
            PuzzleSelectionManager.Next();
        }
        else
        {         
            PuzzleSelectionManager.Previous();
        }
    }

    public void Dispose()
    {
        PuzzleSelectionManager.OnPuzzleSelectionChanged -= RefreshState;
        ActivePuzzlePieceUpdatedEvent.UnSubscribe(RefreshState);
    }
}
