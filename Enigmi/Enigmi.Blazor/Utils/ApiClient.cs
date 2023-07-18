using System.Net;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.Toast.Services;
using Domain.ValueObjects;
using Enigmi.Blazor.Events;
using Enigmi.Common.Utils;
using Enigmi.Messages;
using static System.FormattableString; 
using Models = Enigmi.Blazor.Shared.Models;


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
                    valueObjectsUtxoAssets.Add(new UtxoAsset(utxo.TxHash.ThrowIfNull(), utxo.TxIndex, assetId, (ulong)asset.Quantity));
                }
            }

            valueObjectsUtxoAssets.Add(new UtxoAsset(utxo.TxHash.ThrowIfNull(), utxo.TxIndex, Constants.LovelaceTokenAssetId, utxo.Balance.Lovelaces));
        }

        walletStateRequest.UtxoAssets = valueObjectsUtxoAssets;
    }

    // TODO: remove mock data

	public List<Models.PuzzleCollection> PuzzleCollections = new()
	{ 
		new Models.PuzzleCollection(new Guid("a6474de9-d687-4e75-9120-f83f121763db"), "Monsters", 10, new List<int>{ 4,9,16 }),
		new Models.PuzzleCollection(new Guid("a2e2350d-18d5-4709-ab76-fed9de13ed9d"), "Rats", 10,new List<int>{ 4,9,16 })
	};

	public List<Models.Puzzle> Puzzles = new()
	{
		new Models.Puzzle(new Guid("5dc40e54-7228-446e-b4ab-8fb8e580d0aa"), new Guid("a6474de9-d687-4e75-9120-f83f121763db"), "A bubble-gum monster sitting in a lazy boy 1", 4, 5, 0) { ImageUrl = "images/temp/5dc40e54-7228-446e-b4ab-8fb8e580d0aa.png" },
		new Models.Puzzle(new Guid("92248276-8eb7-4c55-8bd5-73fe8a944de8"), new Guid("a6474de9-d687-4e75-9120-f83f121763db"), "A bubble-gum monster sitting in a lazy boy 2", 4, 5, 0) { ImageUrl = "images/temp/92248276-8eb7-4c55-8bd5-73fe8a944de8.png" },
		new Models.Puzzle(new Guid("443a6f09-4d05-4fd5-894e-3b22dd4e39be"), new Guid("a6474de9-d687-4e75-9120-f83f121763db"), "A bubble-gum monster sitting in a lazy boy 3", 4, 1, 1) { ImageUrl = "images/temp/443a6f09-4d05-4fd5-894e-3b22dd4e39be.png" }
	};

	public List<Models.PuzzlePiece> PuzzlesPieces = new()
	{ 
		new Models.PuzzlePiece(new Guid("af0b571f-4ec7-4a29-8256-87e934c21d3d"), new Guid("5dc40e54-7228-446e-b4ab-8fb8e580d0aa"), "images/temp/af0b571f-4ec7-4a29-8256-87e934c21d3d.png", 0, false,0),
		new Models.PuzzlePiece(new Guid("a5203f29-5bc0-4b5b-9a34-90f9569aeed1"), new Guid("5dc40e54-7228-446e-b4ab-8fb8e580d0aa"), "images/temp/a5203f29-5bc0-4b5b-9a34-90f9569aeed1.png", 1, false,0),
		new Models.PuzzlePiece(new Guid("0d9fce4a-750e-4f56-8f3b-7d33e4f4013d"), new Guid("5dc40e54-7228-446e-b4ab-8fb8e580d0aa"), "images/temp/0d9fce4a-750e-4f56-8f3b-7d33e4f4013d.png", 2, false,0),
		new Models.PuzzlePiece(new Guid("9ed2bc94-8526-40c7-9cd4-7fe188b1b35e"), new Guid("5dc40e54-7228-446e-b4ab-8fb8e580d0aa"), "images/temp/9ed2bc94-8526-40c7-9cd4-7fe188b1b35e.png", 3, false,0),
		new Models.PuzzlePiece(new Guid("38e0309c-1d86-4a1b-b590-4ebc8480668c"), new Guid("92248276-8eb7-4c55-8bd5-73fe8a944de8"), "images/temp/38e0309c-1d86-4a1b-b590-4ebc8480668c.png", 0, false,0),
		new Models.PuzzlePiece(new Guid("b40de17a-ec8d-4542-8896-6e6e853eba50"), new Guid("92248276-8eb7-4c55-8bd5-73fe8a944de8"), "images/temp/b40de17a-ec8d-4542-8896-6e6e853eba50.png", 1, false,0),
		new Models.PuzzlePiece(new Guid("b60f93d8-4b5c-4d30-8b5a-9966213c229e"), new Guid("92248276-8eb7-4c55-8bd5-73fe8a944de8"), "images/temp/b60f93d8-4b5c-4d30-8b5a-9966213c229e.png", 2, false,0),
		new Models.PuzzlePiece(new Guid("385d3bae-1679-4ccc-9fca-df2c52fea105"), new Guid("92248276-8eb7-4c55-8bd5-73fe8a944de8"), "images/temp/385d3bae-1679-4ccc-9fca-df2c52fea105.png", 3, false,0),
		new Models.PuzzlePiece(new Guid("0dccc352-5553-4af1-8212-0bff2036f5ff"), new Guid("443a6f09-4d05-4fd5-894e-3b22dd4e39be"), "images/temp/0dccc352-5553-4af1-8212-0bff2036f5ff.png", 0, false,0),
		new Models.PuzzlePiece(new Guid("8fbc74fa-b1f0-47a5-8a51-dc37b13805da"), new Guid("443a6f09-4d05-4fd5-894e-3b22dd4e39be"), "images/temp/8fbc74fa-b1f0-47a5-8a51-dc37b13805da.png", 1, false,0),
		new Models.PuzzlePiece(new Guid("4ed1f16e-058d-4098-ad27-7c7c26bc2493"), new Guid("443a6f09-4d05-4fd5-894e-3b22dd4e39be"), "images/temp/4ed1f16e-058d-4098-ad27-7c7c26bc2493.png", 2, false,0),
		new Models.PuzzlePiece(new Guid("c4842e2e-3482-4be3-ba8d-5e52e3fd3b98"), new Guid("443a6f09-4d05-4fd5-894e-3b22dd4e39be"), "images/temp/c4842e2e-3482-4be3-ba8d-5e52e3fd3b98.png", 3, false,0)
	};
}