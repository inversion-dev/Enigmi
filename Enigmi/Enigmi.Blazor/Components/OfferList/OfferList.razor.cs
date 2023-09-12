using Enigmi.Blazor.Shared.Models;
using Enigmi.Blazor.Utils;
using Enigmi.Messages.UserWallet;
using Microsoft.AspNetCore.Components;
using static Enigmi.Messages.ActivePuzzlePieceList.GetPotentialTradesResponse;

namespace Enigmi.Blazor.Components.OfferList;

public partial class OfferList
{
    [Inject]
    private PuzzleSelectionManager PuzzleSelectionManager { get; set; } = null!;

    [Inject]
    public WalletConnection WalletConnection { get; set; } = null!;

    [Inject]
    public ActiveTradesManager ActiveTradesManager { get; set; } = null!;

    [Parameter, EditorRequired]
    public List<string> OfflineStakingAddresses { get; set; } = new List<string>();

    [Parameter]
    public List<GetActiveTradeListResponse.Trade>? Trades { get; set; }

    [Parameter]
    public bool IsLoading { get; set; } = false;

    [Parameter, EditorRequired]
    public ViewMode ViewMode { get; set; }

    private List<UserPuzzle>? Puzzles => PuzzleSelectionManager.AllPuzzles;

    public TradeParty GetParty(TradePartyType tradeParty, TradeDetail tradeDetail)
    {
        var address = WalletConnection.SelectedStakingAddress;        
        var initiatingParty = tradeDetail.InitiatingParty.StakingAddress == address ? tradeDetail.InitiatingParty : tradeDetail.Counterparty;
        var counterParty = tradeDetail.InitiatingParty.StakingAddress == address ? tradeDetail.Counterparty : tradeDetail.InitiatingParty;

        return tradeParty == TradePartyType.Initiating ? initiatingParty : counterParty;
    }

    private Task ViewTrade(GetActiveTradeListResponse.Trade trade)
    {
        var party = GetParty(TradePartyType.Counterparty, trade.TradeDetails);
        var disabled = OfflineStakingAddresses.Contains(party.StakingAddress);

        if (disabled)
        {
            return Task.CompletedTask;
        }

        ActiveTradesManager.SetSelectedTradeDetail(trade);        
        return Task.CompletedTask;
    }
}