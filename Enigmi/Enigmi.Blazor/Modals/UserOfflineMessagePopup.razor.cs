using Blazored.LocalStorage;
using Blazored.Modal;
using Blazored.Toast.Services;
using Enigmi.Blazor.Events;
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

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILocalStorageService LocalStorageService { get; set; } = null!;

    [Inject]
    private OnNicknameUpdatedEvent OnNicknameUpdatedEvent { get; set; } = null!;

    public string? Nickname { get; set; }
  
    public bool IsValid => !string.IsNullOrEmpty(Nickname);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var storedNickName = await LocalStorageService.GetItemAsync<string>(Constants.NicknameStorageKey);
        if (!string.IsNullOrEmpty(storedNickName) && string.IsNullOrEmpty(Nickname))
        {
            Nickname = storedNickName;
            StateHasChanged();
        }

        base.OnAfterRender(firstRender);
    }

    public async Task GoOnline()
    {
        var stakingAddress = WalletConnection.SelectedStakingAddress;
        if (stakingAddress == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(Nickname))
        {
            ToastService.ShowError("Nickname is required");
            return;
        }

        var response = await ApiClient.SendAsync(new ConnectUserCommand(stakingAddress, Nickname));        
        if (response != null)
        {
            await LocalStorageService.SetItemAsync(Constants.NicknameStorageKey, Nickname);
            OnNicknameUpdatedEvent.Trigger();
        }
        await BlazoredModal.CloseAsync();
    }
}
