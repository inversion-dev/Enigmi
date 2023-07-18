namespace Enigmi.Grains.Shared.SystemWallet;

public interface ISystemWalletGrain : IGrainWithIntegerKey
{
    ValueTask<string> GetHumanFriendlyAddress();
}