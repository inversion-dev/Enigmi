using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using Blazored.Toast.Services;
using CardanoSharp.Wallet.CIPs.CIP30.Models;
using Enigmi.Blazor.Events;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Messages.UserWallet;

namespace Enigmi.Blazor.Utils;

public class AuthenticationClient : IDisposable
{
    public IToastService ToastService { get; }

    private OnUnblockScreenRequestedEvent OnUnblockScreenRequestedEvent { get; }
    
    public OnShowScreenBlockerEvent OnShowScreenBlockerEvent { get; }

    private HttpClient Http { get; }

    private ClientAppSettings ClientAppSettings { get; }

    private ILocalStorageService LocalStorageService { get; }

    private WalletConnection WalletConnectorService { get; }
    
    private bool IsAuthenticating { get; set; }

    private object IsReconnectingLock { get; } = new object();

    private bool _isAuthenticated = false;

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (_isAuthenticated == value) return;
            _isAuthenticated = value;
            this.OnAuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? OnAuthenticationStateChanged;

    public event EventHandler? OnAuthenticationStarted;

    public event EventHandler? OnAuthenticationCompleted;


    public AuthenticationClient(WalletConnection walletConnection, 
        ILocalStorageService localStorageService, 
        HttpClient http,
        ClientAppSettings clientAppSettings,
        IToastService toastService,
        OnUnblockScreenRequestedEvent onUnblockScreenRequestedEvent,
        OnShowScreenBlockerEvent onShowScreenBlockerEvent)
    {
        ToastService = toastService.ThrowIfNull();
        Http = http.ThrowIfNull();
        ClientAppSettings = clientAppSettings.ThrowIfNull();
        LocalStorageService = localStorageService.ThrowIfNull();
        WalletConnectorService = walletConnection.ThrowIfNull();
        WalletConnectorService.OnWalletConnected += WalletConnectorService_OnWalletConnected;
        WalletConnectorService.OnWalletDisconnected += WalletConnectorService_OnWalletDisconnected;
        OnUnblockScreenRequestedEvent = onUnblockScreenRequestedEvent.ThrowIfNull();
        OnShowScreenBlockerEvent = onShowScreenBlockerEvent.ThrowIfNull();
    }

    public void Dispose()
    {
        WalletConnectorService.OnWalletConnected -= WalletConnectorService_OnWalletConnected;
        WalletConnectorService.OnWalletDisconnected -= WalletConnectorService_OnWalletDisconnected;
    }

    private async void WalletConnectorService_OnWalletDisconnected(object? sender, EventArgs e)
    {
        await Logout();
    }

    private async void WalletConnectorService_OnWalletConnected(object? sender, EventArgs e)
    {
        await Authenticate();
    }

    public async Task<string?> GetJwtToken()
    {
        return await LocalStorageService.GetItemAsync<string?>(Constants.JwtToken);
    }
    
    public async Task ReAuthenticate()
    {
        await Logout();
        await Authenticate();
    }

    private async Task Logout()
    {
        IsAuthenticated = false;
        await LocalStorageService.RemoveItemAsync(Constants.JwtToken);
    }

    public async Task Authenticate()
    {
        if (IsAuthenticated)
        {
            return;
        }

        if (WalletConnectorService.WalletConnector == null)
        {
            ToastService.ShowError("Wallet connector not set");
            return;
        }

        var token = await LocalStorageService.GetItemAsync<string?>(Constants.JwtToken);
        if (!string.IsNullOrEmpty(token))
        {
            var stakingAddressSet = await WalletConnectorService.SetStakingAddress();
            if (!stakingAddressSet)
            {
                return;
            }

            IsAuthenticated = true;
            return;
        }        

        lock (IsReconnectingLock)
        {
            if (IsAuthenticating)
            {
                return;
            }
            IsAuthenticating = true;
            OnAuthenticationStarted?.Invoke(this, EventArgs.Empty);
        }

        try
        {
            OnShowScreenBlockerEvent.Trigger("Please sign the message using your web wallet");
            var address = await WalletConnectorService.GetRewardAddress();
            if (address == null)
            {
                ToastService.ShowError("Could not find any reward address");
                return;
            }

            var payload = "Sign the message to authenticate";
            var hexMessage = Convert.ToHexString(Encoding.UTF8.GetBytes(payload));
            var hexAddress = address.ToStringHex();
            var signature = await WalletConnectorService!.WalletConnector!.SignData(hexAddress, hexMessage);

            if (string.IsNullOrEmpty(signature.Key) || string.IsNullOrEmpty(signature.Signature))
            {
                return;
            }

            var response = await SendAuthenticateCommand(hexAddress, payload, signature);
            if (response != null)
            {
                var stakingAddressSet = await WalletConnectorService.SetStakingAddress();
                if (!stakingAddressSet)
                {
                    return;
                }

                await LocalStorageService.SetItemAsync(Constants.JwtToken, response.Token);
                IsAuthenticated = true;
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError(ex.Message);
        }
        finally
        {
            IsAuthenticating = false;
            OnUnblockScreenRequestedEvent.Trigger();
            OnAuthenticationCompleted?.Invoke(this,EventArgs.Empty);
        }
    }

    private async Task<AuthenticateResponse?> SendAuthenticateCommand(string hexAddress, string payload, DataSignature signature)
    {
        signature.ThrowIfNull();
        var command = new AuthenticateCommand(hexAddress.ThrowIfNullOrWhitespace(), payload, signature.Key.ThrowIfNull(), signature.Signature.ThrowIfNull());

        var httpRequest = new HttpRequestMessage()
        {
            RequestUri = new Uri($"{ClientAppSettings.ApiUrl}send-message", UriKind.RelativeOrAbsolute),
            Method = HttpMethod.Post,
        };

        httpRequest.Headers.Add("MessageName", command.GetType().FullName);
        httpRequest.Content = JsonContent.Create((object)command);
        using var httpResponse = await Http.SendAsync(httpRequest);
        var responseText = await httpResponse.Content.ReadAsStringAsync();
        var response = JsonSerializer.Deserialize<ResultOrError<AuthenticateResponse>>(responseText, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (response == null)
        {
            ToastService.ShowError("Unable to deserialize message");
            return default;
        }

        var obj = response.GetResultOrError();
        if (obj is ErrorMessage errorMessage)
        {
            ToastService.ShowError(errorMessage.Message);
            return default;
        }
        
        return (AuthenticateResponse)obj;
    }
}