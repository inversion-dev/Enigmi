using Blazored.Toast.Services;
using Enigmi.Blazor.Events;
using Enigmi.Blazor.Utils;
using Enigmi.Domain.ValueObjects;
using Enigmi.Messages.SignalRMessage;
using Enigmi.Messages.UserWallet;
using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Components.TradeLists;

public partial class TradeLists : IDisposable
{
    [Inject] 
    private OnRequestOfferMadeListEvent OnRequestOfferMadeListEvent { get; set; } = null!;

    [Inject]
    private SignalRClient SignalRClient { get; set; } = null!;

    [Inject]
    public WalletConnection WalletConnection { get; set; } = null!;

    [Inject]
    public ActiveTradesManager ActiveTradesManager { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    private int UnseenOffersCount { get; set; } = 0;

    private int InitiatingSignTradeCountdown { get; set; } = 0;
    
    private int TradeTimeoutInSeconds { get; set; }

    private TradePanelTab ActiveTab { get; set; } = TradePanelTab.Unknown;    

    public IDisposable? OfferReceivedSubscription { get; private set; }

    public IDisposable? OfferMadeSubscription { get; private set; }

    public IDisposable? TradeUpdatedSubscription { get; private set; }

    public IDisposable? TradeStakingAddressWentOfflineSubscription { get; private set; }

    public IDisposable? NotifyUserAboutOfflineStateSubscription { get; private set; }    

    public List<string> OfflineStakingAddresses { get; set; } = new List<string>();
    
    private System.Timers.Timer CountdownRefreshTimer { get; set; } = new System.Timers.Timer(1000);

    private System.Timers.Timer ClearUnseenOffersTimer { get; set; } = new System.Timers.Timer(5000);

    private List<Guid> TimedoutOrderIds = new List<Guid>();


    private void ResetView()
    {
        TimedoutOrderIds.Clear();
        this.ActiveTradesManager.Clear();
        OfflineStakingAddresses.Clear();
    }

    private void SetActiveTab(TradePanelTab tab)
    {
        ActiveTab = tab;

        if (ActiveTab == TradePanelTab.OffersReceived)
        {
            UnseenOffersCount = 0;
        }

        ActiveTradesManager.RequestActiveTradeList();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            ActiveTradesManager.RequestActiveTradeList();
            ActiveTradesManager.OnLoadingStateChanged += ActiveTradesManager_OnLoadingStateChanged;

            ClearUnseenOffersTimer.Elapsed += (sender, args) =>
            {
                ClearUnseenOffersTimer.Stop();
                UnseenOffersCount = 0;
                StateHasChanged();
            };

            CountdownRefreshTimer.Elapsed += (sender, args) =>
            {
                if (ShouldCountdownTimerBeActive())
                {
                    var mostUrgentOfferMade = GetMostUrgentOfferMade();
                    if (mostUrgentOfferMade != null)
                    {
                        InitiatingSignTradeCountdown = Convert.ToInt32(this.ActiveTradesManager.TimeLeft(mostUrgentOfferMade)?.TotalSeconds ?? 0);
                        TradeTimeoutInSeconds = mostUrgentOfferMade.TradeTimeoutInSeconds;
                    }
                }
                else
                {
                    InitiatingSignTradeCountdown = 0;
                    CountdownRefreshTimer.Stop();
                }

                NotifyUserOfExpiredTrades();
                StateHasChanged();
            };            
        }
        base.OnAfterRender(firstRender);
    }

    private void NotifyUserOfExpiredTrades()
    {
        var expiredOffersMade = this.ActiveTradesManager.OffersMade?.Where(x =>
                        x.State == TradeState.CounterpartySigned
                        && !TimedoutOrderIds.Contains(x.Id)
                        && (this.ActiveTradesManager.TimeLeft(x)?.TotalSeconds ?? 0) <= 0);

        if (expiredOffersMade != null)
        {
            foreach (var item in expiredOffersMade)
            {
                ToastService.ShowError("Deadline expired to sign trade");
                TimedoutOrderIds.Add(item.Id);
            }
        }

        var expiredOffersReceived = this.ActiveTradesManager.OffersReceived?.Where(x =>
        x.State == TradeState.CounterpartySigned
        && !TimedoutOrderIds.Contains(x.Id)
        && (this.ActiveTradesManager.TimeLeft(x)?.TotalSeconds ?? 0) <= 0);

        if (expiredOffersReceived != null)
        {
            foreach (var item in expiredOffersReceived)
            {
                TimedoutOrderIds.Add(item.Id);
                ToastService.ShowError("Counter party failed to sign trade before the deadline");
            }
        }
    }

    private void ActiveTradesManager_OnLoadingStateChanged(object? sender, EventArgs e)
    {
        OfflineStakingAddresses.Clear();
        TimedoutOrderIds.Clear();

        if (!this.ActiveTradesManager.IsLoading)
        {
            TimedoutOrderIds.Clear();
            StartCountdownTimerIfRequired();
        }                

        StateHasChanged();
    }

    private void StartCountdownTimerIfRequired()
    {        
        if (ShouldCountdownTimerBeActive())
        {
            CountdownRefreshTimer.Start();

            var mostUrgentOfferMade = GetMostUrgentOfferMade();
            if (mostUrgentOfferMade != null)
            {
                InitiatingSignTradeCountdown = Convert.ToInt32(this.ActiveTradesManager.TimeLeft(mostUrgentOfferMade)?.TotalSeconds ?? 0);             
            }
        }        
        else
        {
            InitiatingSignTradeCountdown = 0;
            CountdownRefreshTimer.Stop();
        }
    }

    private bool ShouldCountdownTimerBeActive()
    {
        var hasActiveSignedRecievedOrders = this.ActiveTradesManager.OffersReceived?.Any(x =>
        x.State == TradeState.CounterpartySigned
        && !TimedoutOrderIds.Contains(x.Id)
        && (this.ActiveTradesManager.TimeLeft(x)?.TotalSeconds ?? 0) > 0);

        var hasActiveOffersMadeOrders = this.ActiveTradesManager.OffersMade?.Any(x =>
                x.State == TradeState.CounterpartySigned
                && (this.ActiveTradesManager.TimeLeft(x)?.TotalSeconds ?? 0) > 0);

        return (hasActiveSignedRecievedOrders ?? false) || (hasActiveOffersMadeOrders ?? false);
    }

    private GetActiveTradeListResponse.Trade? GetMostUrgentOfferMade()
    {
        var mostUrgentOfferMade = this.ActiveTradesManager.OffersMade?.Where(x =>
                x.State == TradeState.CounterpartySigned
                && (this.ActiveTradesManager.TimeLeft(x)?.TotalSeconds ?? 0) > 0)
            .MinBy(x => this.ActiveTradesManager.TimeLeft(x)?.TotalSeconds ?? 0);
        
        return mostUrgentOfferMade;
    }

    protected override void OnInitialized()
    {
        OnRequestOfferMadeListEvent.Subscribe(OnRequestActiveTradeList);
        OfferReceivedSubscription = SignalRClient.On((OfferReceived offerReceived) =>
        {
            Console.WriteLine("Offer Received");            
            UnseenOffersCount++;

            if (ActiveTab == TradePanelTab.OffersReceived)
            {
                ClearUnseenOffersTimer.Start();
            }

            ActiveTradesManager.RequestActiveTradeList();
        });

        OfferMadeSubscription = SignalRClient.On((OfferMade offerMade) =>
        {
            if (ActiveTab == TradePanelTab.OffersMade)
            {
                ActiveTradesManager.RequestActiveTradeList();
            }
            else
            {
                SetActiveTab(TradePanelTab.OffersMade);
            }            
        });


        TradeUpdatedSubscription = SignalRClient.On((TradeUpdated tradeUpdated) =>
        {
            Console.WriteLine("Trade Updated");            
            ActiveTradesManager.RequestActiveTradeList();
            StateHasChanged();
        });

        TradeStakingAddressWentOfflineSubscription = SignalRClient.On((TradeStakingAddressWentOffline tradeStakingAddressWentOffline) =>
        {            
            OfflineStakingAddresses.Add(tradeStakingAddressWentOffline.StakingAddress);
            StateHasChanged();
        });

        NotifyUserAboutOfflineStateSubscription = SignalRClient.On((NotifyUserAboutOfflineState offlineState) =>
        {
            ResetView();
            StateHasChanged();
        });

        ActiveTab = TradePanelTab.OffersReceived;        
        base.OnInitialized();
    }

    private void OnRequestActiveTradeList(object? sender, EventArgs e)
    {        
        SetActiveTab(TradePanelTab.OffersMade);
        ActiveTradesManager.RequestActiveTradeList();
        StateHasChanged();
    }

    public void Dispose()
    {
        OnRequestOfferMadeListEvent.UnSubscribe(OnRequestActiveTradeList);
        OfferReceivedSubscription?.Dispose();
        OfferMadeSubscription?.Dispose();
        TradeStakingAddressWentOfflineSubscription?.Dispose();
        NotifyUserAboutOfflineStateSubscription?.Dispose();
        TradeUpdatedSubscription?.Dispose();
    }    
}

public enum TradePanelTab
{
    Unknown,
    OffersReceived,
    OffersMade,
    TradeHistory
}
