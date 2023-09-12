using Blazored.Toast.Services;
using Enigmi.Blazor.Events;
using Enigmi.Messages.UserWallet;

namespace Enigmi.Blazor.Utils;

public class ActiveTradesManager
{
    public ActiveTradesManager(ApiClient apiClient, 
        IToastService toastService, 
        WalletConnection walletConnection, 
        OnTradeViewRequestedEvent onTradeViewRequestedEvent,
        PuzzleSelectionManager puzzleSelectionManager)
    {
        ApiClient = apiClient;
        ToastService = toastService;
        WalletConnection = walletConnection;
        OnTradeViewRequestedEvent = onTradeViewRequestedEvent;
        PuzzleSelectionManager = puzzleSelectionManager;
    }

    private ApiClient ApiClient { get; }

    private IToastService ToastService { get; }

    private WalletConnection WalletConnection { get; }

    private OnTradeViewRequestedEvent OnTradeViewRequestedEvent { get; }

    private PuzzleSelectionManager PuzzleSelectionManager { get; }

    public List<GetActiveTradeListResponse.Trade>? OffersMade { get; set; }

    public List<GetActiveTradeListResponse.Trade>? OffersReceived { get; set; }

    public event EventHandler? OnLoadingStateChanged;

    public DateTime? LastUpdate { get; set; }

    bool _isLoading = false;
    public bool IsLoading
    {
        get
        {
            return _isLoading;
        }

        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnLoadingStateChanged?.Invoke(this, EventArgs.Empty);            
        }
    }

    //public GetActiveTradeListResponse.Trade? SelectedTrade { get; set; }

    public Guid? TradeId { get; private set; }

    public async void RequestActiveTradeList()
    {
        Clear();
        IsLoading = true;        

        try
        {
            var stakingAddress = WalletConnection.SelectedStakingAddress;
            var activeTradeListResponse = await this.ApiClient.SendAsync(new GetActiveTradeListRequest(stakingAddress));
            if (activeTradeListResponse == null)
            {
                return;
            }

            OffersReceived = activeTradeListResponse.OffersReceived;
            OffersMade = activeTradeListResponse.OffersMade;

            LastUpdate = DateTime.UtcNow;

            /*if (SelectedTrade != null)
            {
                //should the trade view be closed when this results in 
                SelectedTrade = OffersReceived.Union(OffersMade).SingleOrDefault(x => x.Id == TradeId); 
            }*/

            var requiredPuzzleDefinitionIds = OffersReceived.Union(OffersMade)
                .Select(x => x.TradeDetails.InitiatingParty.PuzzleDefinitionId).Union(
                    OffersReceived.Union(OffersMade).Select(x => x.TradeDetails.Counterparty.PuzzleDefinitionId))
                .Distinct();
            
            await PuzzleSelectionManager.EnsurePuzzleDefinitionsAreLoaded(requiredPuzzleDefinitionIds);
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }        
    }

    public void Clear()
    {
        OffersMade?.Clear();
        OffersReceived?.Clear();        
    }

    public void SetSelectedTradeDetail(GetActiveTradeListResponse.Trade trade)
    {
        //SelectedTrade = trade;
        TradeId = trade.Id;
        OnTradeViewRequestedEvent.Trigger(trade);
    }

    public void ClearSelectedTradeDetail()
    {
        //SelectedTrade = null;
        TradeId = null;
    }
    
    public TimeSpan? TimeLeft (GetActiveTradeListResponse.Trade? trade) => 
        (trade != null 
         && trade.InitiatingPartySignUtcDeadline.HasValue
         && trade.State == Domain.ValueObjects.TradeState.CounterpartySigned) 
            ? trade.InitiatingPartySignUtcDeadline.Value - (trade.ServerUtcDateTime + (DateTime.UtcNow - LastUpdate))
            : null;
}
