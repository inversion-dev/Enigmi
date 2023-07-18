using Blazored.Modal;
using Enigmi.Blazor.Utils;
using Enigmi.Messages.UserWallet;
using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Modals;

public partial class UserOfflineMessagePopup
{
    [CascadingParameter] 
    BlazoredModalInstance BlazoredModal { get; set; } = default!;

    [Inject] 
    public ApiClient ApiClient { get; set; } = null!;

    [Inject] 
    private WalletConnection WalletConnection { get; set; } = null!;

    public async Task GoOnline()
    {
        var stakeAddress = await WalletConnection.GetRewardAddress();
        if (stakeAddress == null)
        {
            return;
        }

        await ApiClient.SendAsync(new ConnectUserCommand(stakeAddress.ToString()));        
        await BlazoredModal.CloseAsync();
    }
}
