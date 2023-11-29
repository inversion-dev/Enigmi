using System.Net;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.Toast.Services;
using CardanoSharp.Wallet.CIPs.CIP14.Extensions;
using Domain.ValueObjects;
using Enigmi.Blazor.Events;
using Enigmi.Common.Utils;
using Enigmi.Messages;
using static System.FormattableString;


namespace Enigmi.Blazor.Utils;

public class ApiClient
{
    private IToastService ToastService { get; }

    private OnUserWalletStateRefreshedEvent OnUserWalletStateRefreshedEvent { get; }

    public WalletConnection WalletConnection { get; }
    
    private AuthenticationClient Authentication { get; }
    
    private ClientAppSettings ClientAppSettings { get; }
    
    private HttpClient Http { get; set; } = null!;

	public ApiClient(HttpClient http,  
        AuthenticationClient authentication, 
        ClientAppSettings clientAppSettings, 
        WalletConnection walletConnection, 
        IToastService toastService,
        OnUserWalletStateRefreshedEvent onUserWalletStateRefreshedEvent)
	{
        ToastService = toastService;
        OnUserWalletStateRefreshedEvent = onUserWalletStateRefreshedEvent;
        WalletConnection = walletConnection.ThrowIfNull();
        Authentication = authentication.ThrowIfNull();
        ClientAppSettings = clientAppSettings.ThrowIfNull();
        Http = http.ThrowIfNull();
	}

	public async Task<TResponse?> SendAsync<TResponse>(IMessage<TResponse> request)
	where TResponse : IResponse
	{
		request.ThrowIfNull(nameof(request));

		var httpRequest = new HttpRequestMessage()
		{
			RequestUri = new Uri($"{ClientAppSettings.ApiUrl}send-message", UriKind.RelativeOrAbsolute),
			Method = HttpMethod.Post,
		};

        var jwtToken = await Authentication.GetJwtToken();
		if (!string.IsNullOrEmpty(jwtToken))
		{
            httpRequest.Headers.Add("Authorization", Invariant($"Bearer {jwtToken}"));	
		}

        if (request is IHasWalletState walletStateRequest)
        {
            await AddWalletStateToMessage<TResponse>(walletStateRequest);
        }
		
		httpRequest.Headers.Add("MessageName", request.GetType().FullName);
		httpRequest.Content = JsonContent.Create((object)request);

		try
		{
			using var httpResponse = await Http.SendAsync(httpRequest);

			if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
			{
				await Authentication.ReAuthenticate();
				return default;
			}

			if (httpResponse.StatusCode == HttpStatusCode.BadRequest)
			{
				var errorMessage = await httpResponse.Content.ReadAsStringAsync();
				ToastService.ShowError(errorMessage);
				return default;
			}

			if (httpResponse.StatusCode == HttpStatusCode.InternalServerError)
			{
				ToastService.ShowError("Unexpected server side occurred");
				return default;
			}

			if (httpResponse.IsSuccessStatusCode)
			{
				var responseText = await httpResponse.Content.ReadAsStringAsync();
				var response = JsonSerializer.Deserialize<ResultOrError<TResponse>>(responseText,
					new JsonSerializerOptions
					{
						PropertyNamingPolicy = JsonNamingPolicy.CamelCase
					});

				if (response == null)
				{
					ToastService.ShowError("Could not de-serialize message");
					return default;
				}

				if (request is IHasWalletState)
				{
					OnUserWalletStateRefreshedEvent.Trigger();
				}

				return response.Result;
			}

			return default(TResponse);
		}
		catch (Exception ex)
		{
			ToastService.ShowError(ex.Message);
			return default(TResponse);
		}
	}

    private async Task AddWalletStateToMessage<TResponse>(IHasWalletState walletStateRequest) where TResponse : IResponse
    {
        var utxos = await this.WalletConnection!.WalletConnector!.GetUtxos();
        var valueObjectsUtxoAssets = new List<UtxoAsset>();

        foreach (var utxo in utxos)
        {
	        if (utxo.Balance.Assets != null)
            {
                foreach (var asset in utxo.Balance.Assets)
                {
                    var assetId = CardanoHelper.GetAssetId(asset.PolicyId, asset.Name);
                    valueObjectsUtxoAssets.Add(new UtxoAsset(utxo.TxHash.ThrowIfNull(), utxo.TxIndex, assetId, (ulong)asset.Quantity, assetId.ToAssetFingerprint()));
                }
            }

            valueObjectsUtxoAssets.Add(new UtxoAsset(utxo.TxHash.ThrowIfNull(), utxo.TxIndex, Constants.LovelaceTokenAssetId, utxo.Balance.Lovelaces, string.Empty));
        }
        
        walletStateRequest.PaymentAddress = WalletConnection.PaymentAddress;
        walletStateRequest.UtxoAssets = valueObjectsUtxoAssets;
    }
}