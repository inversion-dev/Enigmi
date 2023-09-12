using Blazored.LocalStorage;
using Enigmi.Blazor.Events;
using Enigmi.Blazor.Utils;
using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Components.Menu;

public partial class Menu : IDisposable
{
    [Inject]
    private OnShowBuyPuzzlePieceSectionRequestedEvent OnShowBuyPuzzlePieceSectionRequestedEvent { get; set; } = null!;

    [Inject] 
    private OnUserWalletStateReceivedEvent OnUserWalletStateReceivedEvent { get; set; } = null!;

    [Inject]
    private OnRequestOfferMadeListEvent OnRequestOfferMadeListEvent { get; set; } = null!;

    [Inject]
    private AuthenticationClient AuthenticationClient { get; set; } = null!;

    [Inject]
    private OnNicknameUpdatedEvent OnNicknameUpdatedEvent { get; set; } = null!;

    [Inject]
    private ILocalStorageService LocalStorageService { get; set; } = null!;

    [Parameter]
	public int OwnedPuzzleCount { get; set; } = 0;

	[Parameter]
	public int OwnedPuzzlePieceCount { get; set; } = 0;

	[Parameter]
	public int LeftoverPuzzlePieceCount { get; set; } = 0;

	[Parameter]
	public int TradeCount { get; set; } = 0;

	[Parameter]
	public int NotificationCount { get; set; } = 0;

	private string? UserName { get; set; }

	private bool IsBuyNewPieceEnabled { get; set; } = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        if (firstRender)
        {
	        IsBuyNewPieceEnabled = AuthenticationClient.IsAuthenticated;
	        StateHasChanged();	        
            AuthenticationClient.OnAuthenticationStateChanged += ClientAuthentication_OnAuthenticationStateChanged;
            OnUserWalletStateReceivedEvent.Subscribe(ClientEventUtilOnOnUserWalletStateReceived);

            var nickname = await LocalStorageService.GetItemAsync<string>(Constants.NicknameStorageKey);
            UserName = nickname;

            OnNicknameUpdatedEvent.Subscribe(OnNicknameUpdated);
        }
    }

    private async void OnNicknameUpdated(object? sender, EventArgs e)
    {
        var nickname = await LocalStorageService.GetItemAsync<string>(Constants.NicknameStorageKey);
        UserName = nickname;
        StateHasChanged();
    }

    private void ClientEventUtilOnOnUserWalletStateReceived(object? sender, WalletStateReceivedEventArgs e)
    {
	    OwnedPuzzlePieceCount = e.OwnedPuzzlePiecesCount;
	    StateHasChanged();
    }

    private void ClientAuthentication_OnAuthenticationStateChanged(object? sender, EventArgs e)
    {	    
        IsBuyNewPieceEnabled = AuthenticationClient.IsAuthenticated;
        StateHasChanged();
    }

    public async Task ShowBuyPuzzlePiece()
    {
	    OnShowBuyPuzzlePieceSectionRequestedEvent.Trigger();
	    await Task.CompletedTask;
    }

    public void Dispose()
    {
        AuthenticationClient.OnAuthenticationStateChanged -= ClientAuthentication_OnAuthenticationStateChanged;
        OnUserWalletStateReceivedEvent.UnSubscribe(ClientEventUtilOnOnUserWalletStateReceived);
        OnNicknameUpdatedEvent.UnSubscribe(OnNicknameUpdated);
    }

    public async Task ShowActiveTrades()
    {
        OnRequestOfferMadeListEvent.Trigger();
        await Task.CompletedTask;
    }
}
