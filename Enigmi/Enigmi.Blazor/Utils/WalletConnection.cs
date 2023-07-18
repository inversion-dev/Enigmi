using CardanoSharp.Blazor.Components;
using CardanoSharp.Wallet.Models.Addresses;
using Enigmi.Common;

namespace Enigmi.Blazor.Utils;

public class WalletConnection
{
    public WalletConnector? WalletConnector { get; set; }

    public event EventHandler? OnWalletConnected;

    public event EventHandler? OnWalletDisconnected;

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

    public void OnWalletDisconnect()
    {
        OnWalletDisconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string?> GetWalletAddress()
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
