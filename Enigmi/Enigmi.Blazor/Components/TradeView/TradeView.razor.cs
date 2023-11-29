using Blazored.Toast.Services;
using Enigmi.Blazor.Events;
using Enigmi.Blazor.Utils;
using Enigmi.Domain.ValueObjects;
using Enigmi.Messages.ActivePuzzlePieceList;
using Enigmi.Messages.SignalRMessage;
using Enigmi.Messages.Trade;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using static Enigmi.Messages.ActivePuzzlePieceList.GetPotentialTradesResponse;

namespace Enigmi.Blazor.Components.TradeView;

public partial class TradeView : IDisposable
{
    [Inject]
    private OnTradeViewRequestedEvent TradeViewRequestedEvent { get; set; } = null!;

    [Inject]
    private PuzzleSelectionManager PuzzleSelectionManager { get; set; } = null!;
    
    [Inject]
    private ApiClient ApiClient { get; set; } = null!;

    [Inject]
    private ClientAppSettings ClientAppSettings { get; set; } = null!;

    [Inject]
    private WalletConnection WalletConnection { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private OnShowScreenBlockerEvent OnShowScreenBlockerEvent { get; set; } = null!;
    
    [Inject]
    private SignalRClient SignalRClient { get; set; } = null!;

    [Inject]
    private OnUnblockScreenRequestedEvent OnUnblockScreenRequestedEvent { get; set; } = null!;

    System.Timers.Timer? CountDownTimer { get; set; }
    
    private Trade? Trade { get; set; }
    
    private GetPotentialTradesResponse.TradeDetail? TradeDetail => Trade?.TradeDetail;

    private PartyPuzzleDetail? PartyPuzzleInformation { get; set; }

    private Tab SelectedTab { get; set; } = Tab.Tab1;

    private TradeParty? SelectParty { get; set; }

    private bool IsVisible { get; set; } = false;    

    private bool IsTradeDetailVisible { get; set; } = true;
    
    private DateTime LastUpdate { get; set; }

    private TimeSpan? TimeLeft =>  
        (Trade is { InitiatingPartySignUtcDeadline: { } } 
         && Trade.TradeState == TradeState.CounterpartySigned) 
            ? Trade.InitiatingPartySignUtcDeadline.Value - (Trade.ServerUtcTime + (DateTime.UtcNow - LastUpdate))
            : null;
    public Guid? SelectedTradeId { get; private set; }

    private IDisposable? TradeUpdatedSubscription { get; set; }

    private IDisposable? TradeAvailabilityChangedSubscription { get; set; }

    protected override void OnInitialized()
    {
        TradeViewRequestedEvent.Subscribe(OnTradeViewRequestedEvent);
        CountDownTimer = new System.Timers.Timer(1000);
        CountDownTimer.Elapsed += CountDownTimer_Elapsed;
        PuzzleSelectionManager.OnUserPuzzlesUpdated += PuzzleSelectionManager_OnUserPuzzlesUpdated;

        TradeUpdatedSubscription = this.SignalRClient.On(async (TradeUpdated @event) =>
        {
            if (SelectedTradeId == @event.TradeId)
            {
                await RefreshTradeInformation();
            }
        });

        TradeAvailabilityChangedSubscription = SignalRClient.On((TradeAvailabilityChanged @event) =>
        {
            if (SelectedTradeId == @event.TradeId && Trade != null)
            {
                Trade = Trade with { IsAvailable = @event.IsAvailable };
                StateHasChanged();
            }
            
        });

        base.OnInitialized();        
    }

    private void PuzzleSelectionManager_OnUserPuzzlesUpdated(object? sender, EventArgs e)
    {
        StateHasChanged();
    }

    private async void OnTradeViewRequestedEvent(object? sender, RequestTradeViewEventArgs e)
    {
        SetActiveTab(Tab.Tab1);
        SelectedTradeId = e.Trade.Id;
        await RefreshTradeInformation();
    }

    private async Task RefreshTradeInformation()
    {
        IsTradeDetailVisible = true;
        IsVisible = true;        
        StateHasChanged();

        if (SelectedTradeId.HasValue)
        {
            var getTradeResponse = await ApiClient.SendAsync(new GetTradeRequest(SelectedTradeId.Value));
            if (getTradeResponse != null)
            {
                LastUpdate = DateTime.UtcNow;
                Trade = getTradeResponse.Trade;
            }
        }
        
        StateHasChanged();
        StartCountdownTimerIfRequired();

        SetActiveTab(SelectedTab);
    }

    private void StartCountdownTimerIfRequired()
    {
        if (TimeLeft?.TotalSeconds > 0)
        {
            CountDownTimer?.Start();
        }
    }

    private void CountDownTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (TimeLeft.HasValue)
        {
            if (TimeLeft.Value.TotalSeconds < 0 && this.CountDownTimer != null)
            {
                this.CountDownTimer.Stop();                
            }
        }
        else
        {
            CountDownTimer?.Stop();    
        }
        StateHasChanged();
    }

    public void Dispose()
    {
        TradeViewRequestedEvent.UnSubscribe(OnTradeViewRequestedEvent);
        if (CountDownTimer != null)
        {
            CountDownTimer.Elapsed -= CountDownTimer_Elapsed;
        }        
        TradeUpdatedSubscription?.Dispose();
        TradeAvailabilityChangedSubscription?.Dispose();
        PuzzleSelectionManager.OnUserPuzzlesUpdated -= PuzzleSelectionManager_OnUserPuzzlesUpdated;
    }

    private async Task SignTrade()
    {        
        if (Trade == null || WalletConnection.WalletConnector == null)
        {
            return;
        }

        if (this.Trade.UnsignedTransactionCborHex == null)
        {
            this.ToastService.ShowError("Empty transaction received to sign");
            return;
        }

        try
        {
            OnShowScreenBlockerEvent.Trigger("Please sign the message using your web wallet");
            var signTxCborResponse = await WalletConnection.WalletConnector.SignTxCbor(this.Trade.UnsignedTransactionCborHex, true);

            var signTradeByCounterpartResponse = await ApiClient.SendAsync(new SignTradeByInitiatingPartyCommand(Trade.Id, signTxCborResponse));
            if (signTradeByCounterpartResponse != null)
            {
                ToastService.ShowSuccess("Successfully signed trade");
                await RefreshTradeInformation();
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message);
        }
        finally
        {
            OnUnblockScreenRequestedEvent.Trigger();
        }

        StateHasChanged();
    }

    private async Task AcceptTrade()
    {
        if (Trade == null || WalletConnection.WalletConnector == null)
        {
            return;
        }

        var buildTransactionResponse = await ApiClient.SendAsync(new BuildTransactionCommand(Trade.Id));
        if (buildTransactionResponse == null)
        {
            return;
        }

        try
        {
            OnShowScreenBlockerEvent.Trigger("Please sign the message using your web wallet");            
            var signTxCborResponse = await WalletConnection.WalletConnector.SignTxCbor(buildTransactionResponse.UnsignedCbor, true);

            var signTradeByCounterpartResponse = await ApiClient.SendAsync(new SignTradeByCounterpartCommand(Trade.Id, signTxCborResponse));
            if (signTradeByCounterpartResponse != null)
            {
                ToastService.ShowSuccess("Successfully signed trade");
                await RefreshTradeInformation();
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message);
        }
        finally
        {
            OnUnblockScreenRequestedEvent.Trigger();
        }

        StateHasChanged();
    }

    private async Task CancelTrade()
    {
        await Task.CompletedTask;
    }

    private void ClosePanel()
    {
        this.SelectedTradeId = null;
        this.Trade = null;
        this.IsVisible = false;
    }

    private string? GetImagePath(TradeParty party)
    {
        if (TradeDetail == null)
        {
            return null;
        }

        var puzzle = PuzzleSelectionManager.AllPuzzles.FirstOrDefault(x => x.PuzzleId == party.PuzzleDefinitionId);
        if (puzzle == null)
        {
            return null;
        }

        var puzzlePiece = puzzle.PuzzlePieces.FirstOrDefault(x => x.PuzzlePieceDefinitionId == party.PuzzlePieceDefinitionId);

        return puzzlePiece?.PuzzlePiece?.ImageUrl;
    }

    public void HideTradeDetail()
    {
        IsTradeDetailVisible = false;
    }

    public async Task ShowTradeDetail()
    {
        IsTradeDetailVisible = true;
        await Task.CompletedTask;
        StateHasChanged();
    }

    public void SetActiveTab(Tab tab)
    {
        SelectedTab = tab;
        if (TradeDetail == null)
        {
            return;
        }

        if (SelectedTab == Tab.Tab1)
        {
            this.SelectParty = TradeDetail.InitiatingParty;

        }
        else if (SelectedTab == Tab.Tab2)
        {
            this.SelectParty = null;
            this.PartyPuzzleInformation = null;
        }
        else if (SelectedTab == Tab.Tab3)
        {
            this.SelectParty = TradeDetail.Counterparty;
        }

        if (this.SelectParty != null)
        {
            var puzzle = PuzzleSelectionManager.AllPuzzles.FirstOrDefault(x => x.PuzzleId == SelectParty.PuzzleDefinitionId);
            if (puzzle != null)
            {
                this.PartyPuzzleInformation = new PartyPuzzleDetail { 
                    PuzzleCollectionTitle = puzzle.CollectionTitle, 
                    PuzzleTitle = puzzle.PuzzleTitle, 
                    PuzzleSize = puzzle.PuzzleSize };
            }
        }

        StateHasChanged();
    }

    public enum Tab
    {
        Tab1,
        Tab2,
        Tab3
    }

    public class PartyPuzzleDetail
    {
        public string PuzzleTitle { get; set; } = string.Empty;
        public string PuzzleCollectionTitle { get; set; } = string.Empty;
        public int PuzzleSize { get; set; }
    }
}