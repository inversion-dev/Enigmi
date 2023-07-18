using Enigmi.Blazor.Utils;
using Enigmi.Common;
using Enigmi.Messages.ActivePuzzlePieceList;
using Microsoft.AspNetCore.Components;
using System.Reflection;
using Models = Enigmi.Blazor.Shared.Models;

namespace Enigmi.Blazor.Components.PuzzlePiece;

public partial class PuzzlePiece
{
    [Parameter, EditorRequired]
    public Models.PuzzlePiece PuzzlePieceItem { get; set; } = null!;

    [Parameter]
    public bool ShowOfferIcon { get; set; } = false;

    [Parameter]
    public bool ShowDuplicateIcon { get; set; } = false;

    [Parameter]
    public string? ImageUrl { get; set; }

    [Parameter]
    public bool Selected { get; set; } = false;

    [Parameter]
    public bool IsOwned { get; set; } = false;

    [Parameter]
    public bool EnableFindPiece { get; set; } = false;

    [Parameter]
	public EventCallback OnPuzzlePieceClicked { get; set; }

    [Inject]
    public ApiClient ApiClient { get; set; } = null!;    

    [Inject]
    public WalletConnection WalletConnection { get; set; } = null!;


    private bool ShowIconlayer => ShowOfferIcon || ShowDuplicateIcon;

    private async Task PuzzlePieceClicked()
    {
        await OnPuzzlePieceClicked.InvokeAsync().ContinueOnAnyContext();
    }  

    private async Task FindPiece()
    {
        var stakingAddress = await WalletConnection.GetRewardAddress();
        if (stakingAddress == null)
        {
            return;
        }

        var tradeResponse = await this.ApiClient.SendAsync(new GetPotentialTradesRequest(stakingAddress.ToString(), PuzzlePieceItem.Id));
    }
}