using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using JsonNet.PrivateSettersContractResolvers;
using Newtonsoft.Json;

namespace Enigmi.Blazor.Utils;



public class SignalRClient
{
    private AuthenticationClient AuthenticationClient { get; }
    private ClientAppSettings ClientAppSettings { get; }
    private HubConnection? HubConnection { get; }
    private object IsReconnectingLock { get; } = new object();
    private bool IsConnecting { get; set; } = false;
    public event EventHandler? OnConnectionStateChanged;
    public bool IsConnected { get; private set; }

    public SignalRClient(ClientAppSettings clientAppSettings, AuthenticationClient authenticationClient)
    {
        AuthenticationClient = authenticationClient.ThrowIfNull();
        ClientAppSettings = clientAppSettings.ThrowIfNull();

        var builder = new HubConnectionBuilder()
            .WithAutomaticReconnect()
            .AddNewtonsoftJsonProtocol(options =>
            {
                options.PayloadSerializerSettings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto, ContractResolver = new PrivateSetterContractResolver() };
            })
            .WithUrl(ClientAppSettings.ApiUrl, options =>
            {
                options.AccessTokenProvider = async () => await AuthenticationClient.GetJwtToken();
            });

        HubConnection = builder.Build();

        HubConnection.Closed += error =>
        {
            IsConnected = false;
            OnConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            return ConnectWithRetryAsync();
        };

        HubConnection.Reconnected += Connection_Reconnected;
        HubConnection.Reconnecting += Connection_Reconnecting;
        authenticationClient.OnAuthenticationStateChanged += ClientAuthentication_OnAuthenticationStateChanged;
    }

    private async void ClientAuthentication_OnAuthenticationStateChanged(object? sender, EventArgs e)
    {
        if (AuthenticationClient.IsAuthenticated)
        {
            await ConnectWithRetryAsync();
        }
        else
        {
            if (this.HubConnection != null)
            {
                if (this.IsConnected)
                {
                    await this.HubConnection.StopAsync();
                }
            }
        }
    }

    private async Task Connection_Reconnecting(Exception? arg)
    {
        IsConnected = false;
        OnConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask.ContinueOnAnyContext();
    }

    private async Task Connection_Reconnected(string? arg)
    {
        IsConnected = true;
        OnConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask.ContinueOnAnyContext();
    }

    public IDisposable On<T>(Action<T> handler) where T : ISignalRMessage
    {
        handler.ThrowIfNull();
        HubConnection.ThrowIfNull();
        
        Console.WriteLine($"subscribed to {typeof(T).Name}");

        return HubConnection.On<T>(typeof(T).Name, msg =>
        {
            try
            {
                handler(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        });
    }

    public IDisposable On<T>(Func<T, Task> handler) where T : ISignalRMessage
    {
        handler.ThrowIfNull();
        HubConnection.ThrowIfNull();

        return HubConnection.On<T>(typeof(T).Name, async msg =>
        {
            try
            {
                await handler(msg).ContinueOnAnyContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        });
    }

    private async Task<bool> ConnectWithRetryAsync()
    {
        if (!AuthenticationClient.IsAuthenticated)
        {
            return false;
        }

        while (!IsConnected)
        {
            try
            {
                var (isConnected, isUnauthorized) = await StartInternal().ContinueOnAnyContext();
                if (isConnected)
                {
                    return true;
                }

                if (isUnauthorized)
                {
                    break;
                }

                await Task.Delay(5000).ContinueOnAnyContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await Task.Delay(5000).ContinueOnAnyContext();
            }
        }

        return IsConnected;
    }

    private async Task<(bool isConnected, bool isUnauthorized)> StartInternal()
    {
        if (!AuthenticationClient.IsAuthenticated)
        {
            return (false, false);
        }

        if (IsConnected)
        {
            return (true, false);
        }

        lock (IsReconnectingLock)
        {
            if (IsConnecting)
            {
                return (false, false);
            }

            IsConnecting = true;
        }

        try
        {
            await HubConnection!.StartAsync().ContinueOnAnyContext();
            IsConnected = HubConnection!.State == HubConnectionState.Connected;
            IsConnecting = false;
            OnConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            Console.WriteLine("Signalr is connected");
            return (IsConnected, false);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                IsConnecting = false;
                await AuthenticationClient.ReAuthenticate();
                return (false, false);
            }
        }
        catch (Exception ex)
        {
            IsConnecting = false;
            Console.WriteLine(ex.Message);
        }

        return (false, false);
    }

    public async Task Connect()
    {
        await StartInternal();
    }
}
