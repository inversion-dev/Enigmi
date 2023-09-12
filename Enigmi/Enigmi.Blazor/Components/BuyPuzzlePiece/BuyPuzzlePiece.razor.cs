using Blazored.Toast.Services;
using Enigmi.Blazor.Utils;
using Enigmi.Common;
using Enigmi.Messages.PuzzleCollection;
using Enigmi.Messages.UserWallet;
using Microsoft.AspNetCore.Components;
using System;
using System.Reflection.Metadata;
using CardanoSharp.Wallet.Models.Addresses;
using Enigmi.Blazor.Events;
using Enigmi.Domain.Entities.OrderAggregate;
using Enigmi.Messages.SignalRMessage;
using Models = Enigmi.Blazor.Shared.Models;
using static System.FormattableString;

namespace Enigmi.Blazor.Components.BuyPuzzlePiece;

public partial class BuyPuzzlePiece : IDisposable
{
    [Inject]
    private ApiClient ApiClient { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject] 
    private SignalRClient SignalRClient { get; set; } = null!;

    [Inject] 
    private OnShowScreenBlockerEvent OnShowScreenBlockerEvent { get; set; } = null!;

    [Inject] 
    private OnUnblockScreenRequestedEvent OnUnblockScreenRequestedEvent { get; set; } = null!;

    [Inject]
    private OnHideBuyPuzzlePieceSectionRequestedEvent OnHideBuyPuzzlePieceSectionRequestedEvent { get; set; } = null!;

    [Inject] 
    private OnUserWalletStateRefreshedEvent OnUserWalletStateRefreshedEvent { get; set; } = null!;

    [Parameter]
    public bool Visible { get; set; }

    [Parameter]
    public EventCallback OnBoughtPuzzlePieceClicked { get; set; }

    [Inject] 
    private WalletConnection WalletConnection { get; set; } = null!;

    [Inject]
    private AuthenticationClient AuthenticationClient { get; set; } = null!;

    private int PuzzleSize { get; set; } = 4;

    private int _quantity = 1;
    private Guid? _selectedPuzzleCollectionId;

    private int Quantity
    {
        get => _quantity;
        set
        {
            _quantity = value;
            QuantityChanged();
        }
    }

    private Guid? ActiveOrderId { get; set; }
    private uint? ActiveOrderNumberOfConfirmations { get; set; }

    private Guid? SelectedPuzzleCollectionId
    {
        get => _selectedPuzzleCollectionId;
        set { _selectedPuzzleCollectionId = value; StateHasChanged(); }
    }

    private Models.PuzzleCollection? SelectedPuzzleCollection => PuzzleCollections?.FirstOrDefault(x => x.Id == SelectedPuzzleCollectionId);

    private List<Models.PuzzleCollection> PuzzleCollections { get; set; } = new();
    private List<Models.PuzzlePiece> BoughtPuzzlePieces { get; set; } = new();
    private List<Models.PuzzlePiece> LeftoverPuzzlePieces { get; set; } = new();
    private List<Models.PuzzlePiece> TradeInPuzzlePieces { get; set; } = new();
    
    private IDisposable? OrderBlockchainStateUpdateSubscription { get; set; }    
    private IDisposable? OrderBlockchainCompletedSubscription { get; set; }
    private IDisposable? OrderBlockchainFailedSubscription { get; set; }

    private enum ViewModeOption { Loading, Draft, Buying, Bought }
    private ViewModeOption ViewMode { get; set; } = ViewModeOption.Draft;

    // TODO: confirm/update statuses
    private enum BuyStatusOption { Ready, Started, ConfirmingBlockchainTransaction, BlockchainTransactionConfirmed, Completed }
    private BuyStatusOption BuyStatus { get; set; } = BuyStatusOption.Ready;

    private bool ShowTradeInPieces { get; set; } = false;

    private decimal? OriginalPrice => Quantity * (SelectedPuzzleCollection?.PuzzlePiecePriceInAda);
    private decimal BuyPrice => Math.Max(0, (OriginalPrice ?? 0) - TradeInPuzzlePieces.Sum(x => x.TradeInValueInAda));
    private decimal TransactionFees { get; set; } = 0;

    private bool BusyBuying => BuyStatus == BuyStatusOption.Started
        || BuyStatus == BuyStatusOption.ConfirmingBlockchainTransaction
        || BuyStatus == BuyStatusOption.BlockchainTransactionConfirmed;

    private string BuyButtonDisplay => BuyStatus switch
    {
        BuyStatusOption.Started => "Buying",
        BuyStatusOption.ConfirmingBlockchainTransaction => "Confirming Blockchain Transaction",
        BuyStatusOption.BlockchainTransactionConfirmed => "Blockchain Transaction Confirmed",
        _ => "Buy",
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        
        OrderBlockchainStateUpdateSubscription = SignalRClient.On( (OrderUpdated message) =>
        {
            if (message.OrderId != ActiveOrderId)
            {
                return;
            }

            ActiveOrderNumberOfConfirmations = message.NumberOfConfirmations;
            StateHasChanged();
            Console.WriteLine(message);
        });
        
        OrderBlockchainFailedSubscription = SignalRClient.On( (OrderFailed message) =>
        {
            if (message.OrderId != ActiveOrderId)
            {
                return;
            }
            
            Console.WriteLine(message);
            ToastService.ShowError("Order failed, please place another");
            ResetBuyState();
        });
        
        OrderBlockchainCompletedSubscription = SignalRClient.On((OrderCompleted message) =>
        {
            if (message.OrderId != ActiveOrderId)
            {
                return;
            }

            Task.Run(ShowCompletedOrder);
        });

        AuthenticationClient.OnAuthenticationStateChanged += ClientAuthentication_OnAuthenticationStateChanged;
        OnUserWalletStateRefreshedEvent.Subscribe(ClientEventUtilOnOnUserWalletStateRefreshed);
        if (AuthenticationClient.IsAuthenticated)
        {
            await LoadData().ContinueOnAnyContext();
        }
    }

    private async void ClientEventUtilOnOnUserWalletStateRefreshed(object? sender, EventArgs e)
    {
        if (ActiveOrderId != null)
        {
            if (await RetrieveBoughtPuzzlePieces())
            {
                ActiveOrderId = null;                
            }

            StateHasChanged();
        }
    }

    private void ShowCompletedOrder()
    {
        ActiveOrderNumberOfConfirmations = null;
        ViewMode = ViewModeOption.Bought;
        BuyStatus = BuyStatusOption.Completed;
        ToastService.ShowSuccess("Your payment has been confirmed");
        StateHasChanged();
    }

    private async Task<bool> RetrieveBoughtPuzzlePieces()
    {
        BoughtPuzzlePieces.Clear();       
        
        var walletState = await ApiClient.SendAsync(new GetStateRequest(WalletConnection.SelectedStakingAddress, ActiveOrderId));
        if (walletState != null)
        {
            var puzzlePieceModels = walletState.PuzzlePieces.Select(x =>
                new Models.PuzzlePiece(
                    x.PuzzlePieceDefinitionId,
                    x.PuzzleDefinitionId,
                    x.ImageUrl,
                    0,
                    true,
                    x.PuzzlePieceCount)
            ).ToList();
            BoughtPuzzlePieces.AddRange(puzzlePieceModels);
        }

        return walletState != null;
    }

    public void Dispose()
    {
        AuthenticationClient.OnAuthenticationStateChanged -= ClientAuthentication_OnAuthenticationStateChanged;
        OrderBlockchainStateUpdateSubscription?.Dispose();
        OrderBlockchainCompletedSubscription?.Dispose();
        OrderBlockchainFailedSubscription?.Dispose();
    }

    private async void ClientAuthentication_OnAuthenticationStateChanged(object? sender, EventArgs e)
    {
        if (AuthenticationClient.IsAuthenticated)
        {
            await LoadData().ContinueOnAnyContext();
        }
    }

    private async Task LoadActiveOrder()
    {
        ViewMode = ViewModeOption.Loading;
        StateHasChanged();       
        

        var activeOrderResponse = await ApiClient.SendAsync(new GetActiveOrderRequest(WalletConnection.SelectedStakingAddress));
        if (activeOrderResponse == null)
        {
            ResetBuyState();
            return;
        }

        if (activeOrderResponse.OrderId == null)
        {
            ResetBuyState();
            return;
        }

        ActiveOrderId = activeOrderResponse.OrderId;
        ActiveOrderNumberOfConfirmations = activeOrderResponse.NumberOfConfirmations;

        if (activeOrderResponse.OrderState is OrderState.TransactionSubmitted or OrderState.TransactionSigned)
        {
            ViewMode = ViewModeOption.Buying;
            BuyStatus = BuyStatusOption.ConfirmingBlockchainTransaction;
            StateHasChanged();
        }
        else
        {
            ResetBuyState();
        }
    }

    private async Task LoadData()
    {
        await LoadActiveOrder();
        await Task.WhenAll(LoadPuzzleCollections(), LoadPuzzlePieces()).ContinueOnAnyContext();
        StateHasChanged();
    }

    private async Task LoadPuzzleCollections()
    {
        var puzzleCollectionResponse = await this.ApiClient.SendAsync(new GetPuzzleCollectionsRequest());
        if (puzzleCollectionResponse == null)
        {
            return;
        }

        PuzzleCollections.Clear();
        PuzzleCollections.AddRange(puzzleCollectionResponse.ResponsePuzzleCollections.Select(x => 
            new Models.PuzzleCollection(
                x.Id, 
                x.Title, 
                x.PuzzlePiecePriceInAda, 
                x.PermittedPuzzleSize)
        ));
        SelectedPuzzleCollectionId = null;
        StateHasChanged();

        AutoSelectFirstCollection();

        await Task.CompletedTask.ContinueOnAnyContext();
    }

    private void AutoSelectFirstCollection()
    {
        if (PuzzleCollections.Count > 0)
        {
            var firstPuzzleCollection = PuzzleCollections.First();
            SelectedPuzzleCollectionId = firstPuzzleCollection.Id;
            PuzzleSize = firstPuzzleCollection.PermittedPuzzleSize.FirstOrDefault();
        }
    }

    private async Task LoadPuzzlePieces()
    {
        // TODO: get via service
        LeftoverPuzzlePieces.Clear();
        StateHasChanged();

        await Task.CompletedTask.ContinueOnAnyContext();
    }

    private async Task BuyPieces()
    {
        if (SelectedPuzzleCollection == null)
        {
            ToastService.ShowError("Please select a puzzle collection first");
            return;
        }

        ViewMode = ViewModeOption.Buying;
        BuyStatus = BuyStatusOption.Started;
        StateHasChanged();

        /*
        // TODO: post/get via service
        await Task.Delay(1000);
        BuyStatus = BuyStatusOption.ConfirmingBlockchainTransaction;
        StateHasChanged();

        await Task.Delay(1000);
        BuyStatus = BuyStatusOption.BlockchainTransactionConfirmed;
        StateHasChanged();

        BoughtPuzzlePieces.Clear();
        var randomPieces = ApiClient.PuzzlesPieces.OrderBy(x => Guid.NewGuid()).Take(Quantity);
        BoughtPuzzlePieces.AddRange(randomPieces);

        LeftoverPuzzlePieces = LeftoverPuzzlePieces.Except(TradeInPuzzlePieces).ToList();
        TransactionFees = 2;

        await Task.Delay(1000);
        ViewMode = ViewModeOption.Bought;
        BuyStatus = BuyStatusOption.Completed;
        StateHasChanged();*/

        if (SelectedPuzzleCollectionId == null || WalletConnection.WalletConnector == null)
        {
            return;
        }


        var response = await PlaceOrder(WalletConnection.SelectedStakingAddress);
        if (response == null)
        {
            ResetBuyState();
            return;
        }

        ActiveOrderId = response.OrderId;
        ActiveOrderNumberOfConfirmations = null;
        
        StateHasChanged();

        try
        {
            await ApproveOrder(response, WalletConnection.SelectedStakingAddress);
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message);
            ResetBuyState();
        }

        StateHasChanged();
    }

    private async Task<CreateOrderResponse?> PlaceOrder(string stakingAddress)
    {
        var paymentAddress = await WalletConnection.WalletConnector!.GetChangeAddress();
        var response = await ApiClient.SendAsync(
            new CreateOrderCommand(
                stakingAddress,
                SelectedPuzzleCollectionId!.Value, 
                PuzzleSize, 
                Quantity)
            {
                PaymentAddress = paymentAddress.ToString()
            }
            );
        

        if (response == null)
        {
            ResetBuyState();
            return null;
        }

        TransactionFees = Convert.ToDecimal(response.TransactionFee) / Constants.LovelacePerAda;

        if (!string.IsNullOrEmpty(response.Warning))
        {
            ToastService.ShowWarning(response.Warning);
        }

        return response;
    }

    private async Task ApproveOrder(CreateOrderResponse response, string stakingAddress)
    {
        try
        {
            OnShowScreenBlockerEvent.Trigger("Please sign the message using your web wallet");
            var witnessSet = await WalletConnection.WalletConnector!.SignTxCbor(response.UnsignedTransactionCborHex!, true);
            var approveResponse = await ApiClient.SendAsync(new ApproveOrderCommand(stakingAddress, response.OrderId, witnessSet));

            if (approveResponse == null)
            {
                ResetBuyState();
                return;
            }

            BuyStatus = BuyStatusOption.ConfirmingBlockchainTransaction;
        }
        finally
        {
            OnUnblockScreenRequestedEvent.Trigger();
        }
    }

    private void ResetBuyState()
    {
        BuyStatus = BuyStatusOption.Ready;
        ViewMode = ViewModeOption.Draft;
        TransactionFees = 0;
        ActiveOrderNumberOfConfirmations = null;
        ActiveOrderId = null;
        StateHasChanged();
    }

    private async Task BoughtPuzzlePieceClicked(Models.PuzzlePiece puzzlePiece)
    {
        await OnBoughtPuzzlePieceClicked.InvokeAsync(puzzlePiece).ContinueOnAnyContext();
    }

    private void QuantityChanged()
    {
        if (OriginalPrice - TradeInPuzzlePieces.Sum(x => x.TradeInValueInAda) >= 0)
            return;

        var piecesToKeep = new List<Models.PuzzlePiece>();
        var total = 0m;

        foreach(var piece in TradeInPuzzlePieces.OrderBy(x => x.TradeInValueInAda))
        {
            total += piece.TradeInValueInAda;

            if (total > OriginalPrice)
                break;

            piecesToKeep.Add(piece);
        }

        TradeInPuzzlePieces = piecesToKeep;
    }

    private void SelectTradeInPuzzlePiece(Models.PuzzlePiece puzzlePiece)
    {
        if (TradeInPuzzlePieces.Any(x => x == puzzlePiece))
            TradeInPuzzlePieces.Remove(puzzlePiece);
        else
        if (BuyPrice - puzzlePiece.TradeInValueInAda >= 0)
            TradeInPuzzlePieces.Add(puzzlePiece);
    }

    private void BuyMore()
    {
        ViewMode = ViewModeOption.Draft;
        BuyStatus = BuyStatusOption.Ready;
        TradeInPuzzlePieces.Clear();
        BoughtPuzzlePieces.Clear();
        TransactionFees = 0;

        StateHasChanged();
    }

    private async Task<string> FadeInStyleClass()
    {
        await Task.Delay(new Random().Next(0, 1000)).ContinueOnAnyContext();
        return "fade-in";
    }

    private void Hide()
    {
        OnHideBuyPuzzlePieceSectionRequestedEvent.Trigger();
        StateHasChanged();
    }
}
