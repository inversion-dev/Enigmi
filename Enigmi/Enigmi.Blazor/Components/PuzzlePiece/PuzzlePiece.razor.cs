using Enigmi.Blazor.Utils;
using Enigmi.Common;
using Microsoft.AspNetCore.Components;
using Enigmi.Blazor.Events;
using Models = Enigmi.Blazor.Shared.Models;

namespace Enigmi.Blazor.Components.PuzzlePiece;

public partial class PuzzlePiece
{
    [Parameter, EditorRequired]
    public Models.PuzzlePiece PuzzlePieceItem { get; set; } = null!;

    [Inject] 
    public OnRequestPotentialTradeListEvent OnRequestPotentialTradeListEvent { get; set; } = null!;

    [Parameter]
    public bool ShowOfferIcon { get; set; } = false;

    [Parameter]
    public int OwnedPuzzlePieceCount { get; set; } = 0;

    [Parameter]
    public string? ImageUrl { get; set; }

    [Parameter]
    public bool Selected { get; set; } = false;

    [Parameter]
    public bool ShowColour { get; set; } = false;

    [Parameter]
    public bool EnableFindPiece { get; set; } = false;

    [Parameter]
	public EventCallback OnPuzzlePieceClicked { get; set; }

    [Parameter]
    public bool ShowIconLayer { get; set; } = true;

    [Parameter]
    public string CustomCss { get; set; } = string.Empty;

    [Inject]
    public ApiClient ApiClient { get; set; } = null!;    

    [Inject]
    public WalletConnection WalletConnection { get; set; } = null!;

    private bool ShowFindPieceButton { get; set; } = false;

    private async Task PuzzlePieceClicked()
    {
        await OnPuzzlePieceClicked.InvokeAsync().ContinueOnAnyContext();
    }  

    private Task FindPiece()
    {
        if (!EnableFindPiece || PuzzlePieceItem.AvailablePuzzlePieceCount == 0)
        {
            return Task.CompletedTask;
        }

        OnRequestPotentialTradeListEvent.Trigger(PuzzlePieceItem.Id);
        return Task.CompletedTask;
    }

    private void MouseEnter()
    {
        ShowFindPieceButton = EnableFindPiece;
    }

    private void MouseLeave()
    {
        ShowFindPieceButton = false;
    }
}