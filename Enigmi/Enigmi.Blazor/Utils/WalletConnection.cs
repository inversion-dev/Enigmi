using Blazored.Toast.Services;
using CardanoSharp.Blazor.Components;
using CardanoSharp.Wallet.Models.Addresses;
using Enigmi.Common;

namespace Enigmi.Blazor.Utils;

public class WalletConnection
{
    public WalletConnection(IToastService toastService)
    {
        ToastService = toastService;
    }

    public WalletConnector? WalletConnector { get; set; }

    public event EventHandler? OnWalletConnected;

    public event EventHandler? OnWalletDisconnected;

    private IToastService ToastService { get; }

    private string? _selectedStakingAddress;
    
    private string? _paymentAddress;
    
    public string PaymentAddress
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_paymentAddress))
            {
                ToastService.ShowError("Staking address have not been set");
            }

            return _paymentAddress.ThrowIfNullOrWhitespace();
        }
    }

    public string SelectedStakingAddress
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_selectedStakingAddress))
            {
                ToastService.ShowError("Staking address have not been set");
            }

            return _selectedStakingAddress.ThrowIfNullOrWhitespace();
        }
    }

    public bool IsConnected => WalletConnector?.Connected ?? false;

    public void ShowWalletSelector()
    {
        WalletConnector?.ShowConnectWalletDialog();
    }

    public void HideWalletSelector()
    {
        WalletConnector?.HideConnectWalletDialog();
    }

    public void OnWalletConnect()
    {  
        OnWalletConnected?.Invoke(this, EventArgs.Empty);        
    }

    public async Task<bool> SetStakingAddress()
    {
        var retrievedAddress = await GetRewardAddress();
        if (retrievedAddress == null)
        {
            this.ToastService.ShowError("Unable to retrieve staking address");
            return false;
        }

        _selectedStakingAddress = retrievedAddress.ToString();
        _paymentAddress = await GetWalletAddress();

        return true;
    }

    public void OnWalletDisconnect()
    {
        _selectedStakingAddress = null;
        _paymentAddress = null;
        OnWalletDisconnected?.Invoke(this, EventArgs.Empty);
    }

    private async Task<string?> GetWalletAddress()
    {
        if (WalletConnector == null)
            return null;

        var address = await WalletConnector.GetChangeAddress().ContinueOnAnyContext();

        return address?.ToString();
    }

    public async Task<Address?> GetRewardAddress()
    {
        if (WalletConnector == null)
            return null;

        return (await WalletConnector.GetRewardAddresses()).FirstOrDefault();
    }
}
