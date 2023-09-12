using Enigmi.Blazor.Shared.Models;
using Enigmi.Blazor.Utils;
using Enigmi.Messages.ActivePuzzlePieceList;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static Enigmi.Messages.ActivePuzzlePieceList.GetPotentialTradesResponse;

namespace Enigmi.Blazor.Components.TradeQuad;

public partial class TradeQuad
{
    private static Random random = new Random();

    [Parameter, EditorRequired]
    public GetPotentialTradesResponse.TradeDetail TradeDetail { get; set; } = null!;    

    [Parameter, EditorRequired] 
    public List<UserPuzzle>? UserPuzzles { get; set; } = new();    

    [Inject]
    public WalletConnection WalletConnection { get; set; } = null!;

    [Parameter]
    public ViewMode ViewMode { get; set; } = ViewMode.OffersMade;

    [Parameter]
    public int? CountdownValue { get; set; }

    [Inject]
    IJSRuntime JsRuntime { get; set; } = null!;    

    private string ElementId = RandomString(10);

    private void OnMouseEnter()
    {
        JsRuntime.InvokeVoidAsync("animateTradingPuzzlePieces", ElementId.ToString());
    }    

    public TradeParty GetParty(TradePartyType tradeParty)
    {
        var address = WalletConnection.SelectedStakingAddress;

        var initiatingParty = TradeDetail.InitiatingParty.StakingAddress == address ? TradeDetail.InitiatingParty : TradeDetail.Counterparty;
        var counterParty = TradeDetail.InitiatingParty.StakingAddress == address ? TradeDetail.Counterparty : TradeDetail.InitiatingParty;        

        if (ViewMode == ViewMode.OffersMade)
        {
            return tradeParty == TradePartyType.Initiating ? initiatingParty : counterParty;            
        }
        else
        {
            return tradeParty == TradePartyType.Initiating ? counterParty : initiatingParty;
        }        
    }

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
