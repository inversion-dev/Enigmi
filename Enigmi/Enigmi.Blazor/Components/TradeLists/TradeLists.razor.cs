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

    private int UnseenOffersCount { get; set; } = 0;

    private int InitiatingSignTradeCountdown { get; set; } = 0;

    private TradePanelTab ActiveTab { get; set; } = TradePanelTab.Unknown;    

    public IDisposable? OfferReceivedSubscription { get; private set; }

    public IDisposable? OfferMadeSubscription { get; private set; }

    public IDisposable? TradeUpdatedSubscription { get; private set; }

    public IDisposable? TradeStakingAddressWentOfflineSubscription { get; private set; }

    public IDisposable? NotifyUserAboutOfflineStateSubscription { get; private set; }    

    public List<string> OfflineStakingAddresses { get; set; } = new List<string>();
    
    private System.Timers.Timer Timer { get; set; } = new System.Timers.Timer(1000);


    private void ResetView()
    {
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
            Timer.Elapsed += (sender, args) =>
            {
                var mostUrgentOfferMade = GetMostUrgentOfferMade();

                if (mostUrgentOfferMade != null)
                {
                    InitiatingSignTradeCountdown = Convert.ToInt32(this.ActiveTradesManager.TimeLeft(mostUrgentOfferMade)?.TotalSeconds ?? 0);
                }
                else
                {
                    InitiatingSignTradeCountdown = 0;
                    Timer.Stop();
                }
                
                StateHasChanged();
            };
        }
        base.OnAfterRender(firstRender);
    }

    private void ActiveTradesManager_OnLoadingStateChanged(object? sender, EventArgs e)
    {
        OfflineStakingAddresses.Clear();

        if (!this.ActiveTradesManager.IsLoading && this.ActiveTradesManager.OffersMade != null)
        {
            StartCountdownTimerIfRequired();
        }        
        else
        {
            InitiatingSignTradeCountdown = 0;
            Timer.Stop();
        }

        StateHasChanged();
    }

    private void StartCountdownTimerIfRequired()
    {
        var mostUrgentOfferMade = GetMostUrgentOfferMade();

        if (mostUrgentOfferMade != null)
        {
            InitiatingSignTradeCountdown = Convert.ToInt32(this.ActiveTradesManager.TimeLeft(mostUrgentOfferMade)?.TotalSeconds ?? 0);
            Timer.Start();
        }
        else
        {
            InitiatingSignTradeCountdown = 0;
            Timer.Stop();
        }
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
