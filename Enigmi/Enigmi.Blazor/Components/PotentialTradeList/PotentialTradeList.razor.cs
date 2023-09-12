using Blazored.Toast.Services;
using Enigmi.Blazor.Events;
using Enigmi.Blazor.Shared.Models;
using Enigmi.Blazor.Utils;
using Enigmi.Messages.ActivePuzzlePieceList;
using Enigmi.Messages.SignalRMessage;
using Enigmi.Messages.UserWallet;
using Microsoft.AspNetCore.Components;
using static System.FormattableString;

namespace Enigmi.Blazor.Components.PotentialTradeList;

public partial class PotentialTradeList : IDisposable
{
    [Inject]
    public OnRequestPotentialTradeListEvent OnRequestPotentialTradeListEvent { get; set; } = null!;

    [Inject]
    public WalletConnection WalletConnection { get; set; } = null!;

    [Inject]
    public ApiClient ApiClient { get; set; } = null!;

    [Inject]
    private PuzzleSelectionManager PuzzleSelectionManager { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private SignalRClient SignalRClient { get; set; } = null!;

    [Inject]
    public OnRequestOfferMadeListEvent OnRequestOfferMadeListEvent { get; set; } = null!;

    private List<UserPuzzle>? Puzzles => PuzzleSelectionManager.Puzzles;

    private List<GetPotentialTradesResponse.UserWalletTradeDetailList>? TradeDetails { get; set; }

    private bool IsLoading { get; set; }

    public bool IsVisible { get; set; } = false;

    public string ShowDropdownMenuForStakingAddress { get; set; } = string.Empty;

    public Dictionary<string,GetPotentialTradesResponse.TradeDetail> SelectedTrades { get; private set; } = new();

    public IDisposable? TradeStakingAddressWentOfflineSubscription { get; private set; }

    public List<string> OfflineStakingAddresses = new List<string>();

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            OnRequestPotentialTradeListEvent.Subscribe(RequestPotentialTradeList);
            TradeStakingAddressWentOfflineSubscription = SignalRClient.On<TradeStakingAddressWentOffline>(message =>
            {
                Console.WriteLine($"TradeStakingAddressWentOffline: {message}");
                OfflineStakingAddresses.Add(message.StakingAddress);


                if (SelectedTrades.ContainsKey(message.StakingAddress))
                {
                    SelectedTrades.Remove(message.StakingAddress);
                }

                StateHasChanged();                
            });
        }

        base.OnAfterRender(firstRender);
    }

    private void ResetView()
    {
        TradeDetails?.Clear();
        SelectedTrades = new();
        ShowDropdownMenuForStakingAddress = string.Empty;
        TradeDetails = new List<GetPotentialTradesResponse.UserWalletTradeDetailList>();
        OfflineStakingAddresses?.Clear();
    }

    private async void RequestPotentialTradeList(object? sender, Guid puzzlePieceDefinitionId)
    {
        ResetView();

        IsVisible = true;                
        IsLoading = true;
        StateHasChanged();

        try
        {
            var stakingAddress = WalletConnection.SelectedStakingAddress;
            if (stakingAddress == null)
            {
                return;
            }

            var tradeResponse = await this.ApiClient.SendAsync(new GetPotentialTradesRequest(stakingAddress.ToString(), puzzlePieceDefinitionId));
            if (tradeResponse == null)
            {
                return;
            }

            TradeDetails = tradeResponse.TradeDetails;
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }

        StateHasChanged();
    }

    public void ClosePanel()
    {
        IsVisible = false;
    }

    public void ToggleDropdownMenu(string stakingAddress)
    {
        if (string.IsNullOrWhiteSpace(ShowDropdownMenuForStakingAddress) || ShowDropdownMenuForStakingAddress != stakingAddress)
        {
            ShowDropdownMenuForStakingAddress = stakingAddress;
        }
        else
        {
            ShowDropdownMenuForStakingAddress = string.Empty;
        }
    }

    public void SelectTrade(string stakingAddress, GetPotentialTradesResponse.TradeDetail tradeDetail)
    {
        if (this.OfflineStakingAddresses.Contains(tradeDetail.Counterparty.StakingAddress))
        {
            return;
        }

        if (tradeDetail == null || stakingAddress == null)
        {
            return;
        }

        if (SelectedTrades.ContainsKey(stakingAddress))
        {
            SelectedTrades[stakingAddress] = tradeDetail;
        }
        else
        {
            SelectedTrades.Add(stakingAddress, tradeDetail);
        }


        ShowDropdownMenuForStakingAddress = string.Empty;
    }

    public async Task MakeAnOffer()
    {
        if (this.SelectedTrades.Count == 0)
        {
            return;
        }
       
        var makeAnOfferResponse = await this.ApiClient.SendAsync(new MakeAnOfferCommand(
            WalletConnection.SelectedStakingAddress,
            this.SelectedTrades.Values.Select(x => new MakeAnOfferCommand.Offer(WalletConnection.SelectedStakingAddress,
                x.InitiatingParty.PuzzlePieceId,
                x.Counterparty.PuzzlePieceId,
                x.Counterparty.StakingAddress,
                x.Counterparty.Nickname)
            ).ToList()));

        if (makeAnOfferResponse != null)
        {
            if (makeAnOfferResponse.SuccessfulOfferCount == 0)
            {
                foreach(var error in makeAnOfferResponse.Errors)
                {
                    this.ToastService.ShowError(error.error);
                }
                
                return;
            }

            if (makeAnOfferResponse.SuccessfulOfferCount < makeAnOfferResponse.OfferCount)
            {
                this.ToastService.ShowWarning(Invariant($"{makeAnOfferResponse.SuccessfulOfferCount} of {makeAnOfferResponse.OfferCount} order were successfully placed"));
            }
            else
            {
                this.ToastService.ShowSuccess("Offer(s) placed successfully");
                            
            }

            IsVisible = false;            
        }

        StateHasChanged();
    }

    public void Dispose()
    {
        OnRequestPotentialTradeListEvent.UnSubscribe(RequestPotentialTradeList);
        TradeStakingAddressWentOfflineSubscription?.Dispose();
    }
}