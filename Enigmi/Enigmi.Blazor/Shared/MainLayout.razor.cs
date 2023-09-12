using Blazored.LocalStorage;
using Blazored.Modal.Services;
using Blazored.Toast.Services;
using CardanoSharp.Blazor.Components;
using Enigmi.Blazor.Events;
using Enigmi.Blazor.Modals;
using Enigmi.Common;
using Enigmi.Messages.SignalRMessage;
using Enigmi.Messages.UserWallet;
using Microsoft.AspNetCore.Components;
using static System.FormattableString;

namespace Enigmi.Blazor.Shared;

public partial class MainLayout : IDisposable
{
    [CascadingParameter] 
    public IModalService? ModalService { get; set; }

    [Inject] 
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private OnUserWalletStateRefreshedEvent OnUserWalletStateRefreshedEvent { get; set; } = null!;

    [Inject]
    private ILocalStorageService LocalStorageService { get; set; } = null!;

    private WalletConnector? WalletConnector { get; set; }
    
    private bool IsInitialized { get; set; }
    
    private PingUser? LastPingUserMessage { get; set; }
    
    private DateTime? LastPingUserMessageReceived { get; set; }
    
    private bool IsUserOfflineMessagePopupVisible { get; set; }
    
    private object UserOfflineMessagePopupLock { get; set; } = new object();

    private bool IsAuthenticating { get; set; }

    private void OnWalletConnectError(Exception ex)
    {
        ToastService.ShowError(ex.Message);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            WalletConnection.WalletConnector = WalletConnector;
            WalletConnection.OnWalletConnected += WalletConnection_OnWalletConnected;
            AuthenticationClient.OnAuthenticationStateChanged += ClientAuthentication_OnAuthenticationStateChanged;
            AuthenticationClient.OnAuthenticationStarted += ClientAuthenticationOnOnAuthenticationStarted;
            AuthenticationClient.OnAuthenticationCompleted += ClientAuthenticationOnOnAuthenticationCompleted;
            TabVisibilityHandler.OnVisibilityChanged += TabFocusHandler_OnVisibilityChanged;

            await TabVisibilityHandler.Load();            
        }
    }

    private void ClientAuthenticationOnOnAuthenticationStarted(object? sender, EventArgs e)
    {
        IsAuthenticating = true;
        StateHasChanged();
    }
    
    private void ClientAuthenticationOnOnAuthenticationCompleted(object? sender, EventArgs e)
    {
        IsAuthenticating = false;
        StateHasChanged();
    }

    public void Dispose()
    {
        WalletConnection.OnWalletConnected -= WalletConnection_OnWalletConnected;
        AuthenticationClient.OnAuthenticationStateChanged -= ClientAuthentication_OnAuthenticationStateChanged;
        TabVisibilityHandler.OnVisibilityChanged -= TabFocusHandler_OnVisibilityChanged;
        AuthenticationClient.OnAuthenticationStarted -= ClientAuthenticationOnOnAuthenticationStarted;
        AuthenticationClient.OnAuthenticationCompleted -= ClientAuthenticationOnOnAuthenticationCompleted;
    }

    private async void TabFocusHandler_OnVisibilityChanged(object? sender, EventArgs e)
    {
        if (LastPingUserMessage == null)
        {
            return;
        }

        if (TabVisibilityHandler.IsVisible)
        {
            var differenceInTimeBetweenClientAndServer = LastPingUserMessage.ServerUtcDateTimeNow - LastPingUserMessageReceived!.Value;
            var now = DateTime.UtcNow.Add(differenceInTimeBetweenClientAndServer);
            if (UserWalletUtil.HasUserWalletOnlineStatusTimedOut(now, LastPingUserMessage.LastRoundTripPingReceivedUtcTimestamp, LastPingUserMessage.IdleTimeout))
            {
                await ShowOfflineStatePopup();                
            }
            else
            {
                await ReplyToPing(null);
            }                
        }
    }

    private async void ClientAuthentication_OnAuthenticationStateChanged(object? sender, EventArgs e)
    {
        if (AuthenticationClient.IsAuthenticated)
        {
            var stakeAddress = WalletConnection.SelectedStakingAddress;
            if (stakeAddress == null)
            {
                return;
            }

            var nickname = await LocalStorageService.GetItemAsync<string>(Constants.NicknameStorageKey);
            if (string.IsNullOrWhiteSpace(nickname))
            {
                await ShowOfflineStatePopup();
                StateHasChanged();
                return;
            }

            await ApiClient.SendAsync(new ConnectUserCommand(stakeAddress.ToString(), nickname));
        }
        
        StateHasChanged();
    }

    private void WalletConnection_OnWalletConnected(object? sender, EventArgs e)
    {
        if (IsInitialized)
        {
            return;
        }

        SignalRClient.On(async (RequestUserWalletStateUpdate message) => await SendUtxosToWalletGrain());
        SignalRClient.On(async (PingUser message) => await ReplyToPing(message));
        SignalRClient.On(async (NotifyUserAboutOfflineState message) =>
        {
            Console.WriteLine("OnNotifyUserAboutOfflineState");
            await ShowOfflineStatePopup();
        });

        SignalRClient.On(async (UserWalletStateHasChanged message) =>
        {
            if (AuthenticationClient.IsAuthenticated)
            {                
                await ApiClient.SendAsync(new RefreshStateCommand(WalletConnection.SelectedStakingAddress));                
            }
        });

        IsInitialized = true;
    }

    private async Task ShowOfflineStatePopup()
    {
        if (IsUserOfflineMessagePopupVisible)
        {
            return;
        }

        if (!AuthenticationClient.IsAuthenticated)
        {
            return;
        }

        lock (UserOfflineMessagePopupLock)
        {            
            IsUserOfflineMessagePopupVisible = true;
        }

        var reference = ModalService?.Show<UserOfflineMessagePopup>("Re-connect");
        if (reference != null)
        {
            await reference.Result;
        }

        lock (UserOfflineMessagePopupLock)
        {
            IsUserOfflineMessagePopupVisible = false;
        }

        await Task.CompletedTask;
    }

    private async Task ReplyToPing(PingUser? message)
    {
        if (message != null)
        {
            LastPingUserMessage = message;
            LastPingUserMessageReceived = DateTime.UtcNow;
        }

        if (!TabVisibilityHandler.IsVisible)
        {
            return;
        }

        if (AuthenticationClient.IsAuthenticated)
        {
            await ApiClient.SendAsync(new ReplyToPingUserWalletCommand(WalletConnection.SelectedStakingAddress));
        }
    }

    private async Task SendUtxosToWalletGrain()
    {   
        await ApiClient.SendAsync(new UpdateUserWalletStateCommand(WalletConnection.SelectedStakingAddress));
    }
}
