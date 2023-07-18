using System.Net;
using CardanoSharp.Blockfrost.Sdk;
using CardanoSharp.Blockfrost.Sdk.Contracts;
using CardanoSharp.Wallet.Extensions;
using Enigmi.Common;
using Enigmi.Infrastructure.Extensions;
using Esendex.TokenBucket;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Refit;
using static Enigmi.Common.Settings;
using static System.FormattableString;

namespace Enigmi.Infrastructure.Services.BlockchainService;

public sealed class BlockfrostBlockchainService : IBlockchainService
{
	private BlockfrostSettings BlockfrostConfig { get; }

	private HttpClient HttpClient { get; }

	private IAccountClient AccountClient { get; }

	private IAssetsClient AssetsClient { get; }

	private IAddressesClient AddressesClient { get; }

	private IBlocksClient BlocksClient { get; }

	private IEpochsClient EpochsClient { get; }

	private ITransactionsClient TransactionsClient { get; }

	private IScriptsClient ScriptsClient { get; }

	private ICacheClient CacheClient { get; }

	private ILogger<BlockfrostBlockchainService> Logger { get; }

	private ITokenBucket TokenBucket { get; }

	public BlockfrostBlockchainService(
		BlockfrostSettings blockfrostConfig,
		HttpClient httpClient,
		IAccountClient accountClient,
		IAssetsClient assetsClient,
		IAddressesClient addressesClient,
		IBlocksClient blocksClient,
		IEpochsClient epochsClient,
		ITransactionsClient transactionsClient,
		IScriptsClient scriptsClient,
		ICacheClient cacheClient,
		ILogger<BlockfrostBlockchainService> logger,
		ITokenBucket tokenBucket)
	{
		BlockfrostConfig = blockfrostConfig.ThrowIfNull();
		HttpClient = httpClient.ThrowIfNull();
		AccountClient = accountClient.ThrowIfNull();
		AssetsClient = assetsClient.ThrowIfNull();
		AddressesClient = addressesClient.ThrowIfNull();
		BlocksClient = blocksClient.ThrowIfNull();
		EpochsClient = epochsClient.ThrowIfNull();
		TransactionsClient = transactionsClient.ThrowIfNull();
		ScriptsClient = scriptsClient.ThrowIfNull();
		CacheClient = cacheClient.ThrowIfNull();
		Logger = logger.ThrowIfNull();
		TokenBucket = tokenBucket.ThrowIfNull();
	}

	public async Task<CardanoSlot> GetLatestSlotAndUtcTimestampAsync()
	{
		var retryResponse = await GetRetryPolicy<Block>()
			.ExecuteAndCaptureAsync(async () =>
			{
				TokenBucket.Consume(1);
				return await BlocksClient.GetLatestBlockAsync().ContinueOnAnyContext();
			}).ContinueOnAnyContext();
		var response = retryResponse.ResultOrFinalOrThrow();
		var block = response.ResultOrThrow();
		return new CardanoSlot(Convert.ToUInt32(block.Slot), DateTime.UnixEpoch.AddSeconds(block.Time));
	}
	
	private AsyncRetryPolicy<ApiResponse<T>> GetRetryPolicy<T>()
	{
		return Policy.HandleResult<ApiResponse<T>>(r => r.Error != null || r.Content == null)
			.WaitAndRetryAsync(
				BlockfrostConfig.MaxRetryAttempts,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				(response, timespan, retryCount, context) =>
				{
					var result = response.Result;
					var error = result?.Error;
					var code = error?.StatusCode ?? HttpStatusCode.UnavailableForLegalReasons;
					var msg = error?.Message ?? "No message available";
					Logger.LogWarning($"ERROR: Pause {timespan.ToString("g")} before retry {retryCount} EX: {code} - {msg}");
				});
	}

	
	
	public async Task<CardanoSlotAndFees> GetSlotAndFeesAsync()
	{
		var blockRetryResponse = await GetRetryPolicy<Block>()
			.ExecuteAndCaptureAsync(async () =>
			{
				TokenBucket.Consume(1);
				return await BlocksClient.GetLatestBlockAsync().ContinueOnAnyContext();
			}).ContinueOnAnyContext();
		var blockResponse = blockRetryResponse.ResultOrFinalOrThrow();
		var block = blockResponse.ResultOrThrow();
		var slot = Convert.ToUInt32(block!.Slot);
		var slotUtcTimestamp = DateTime.UnixEpoch.AddSeconds(block.Time);

		Logger.LogInformation($"Blockfrost Current Slot - Slot: {slot} Slot UTC: {slotUtcTimestamp} UTC Now: {DateTime.UtcNow}");

		var epochRetryResponse = await GetRetryPolicy<EpochParameters>()
			.ExecuteAndCaptureAsync(async () =>
			{
				TokenBucket.Consume(1);
				return await EpochsClient.GetLatestParamtersAsync().ContinueOnAnyContext();
			}).ContinueOnAnyContext();
		var epochResponse = epochRetryResponse.ResultOrFinalOrThrow();
		var epochParams = epochResponse.ResultOrThrow();
		var minFeeA = epochParams.MinFeeA;
		var minFeeB = epochParams.MinFeeB;

		return new CardanoSlotAndFees (new CardanoSlot(slot, slotUtcTimestamp) , new CardanoNetworkFee(minFeeA, minFeeB));
	}

    public async Task<string?> SubmitTransactionAsync(string transactionCbor)
    {
        transactionCbor.ThrowIfNullOrWhitespace();
        byte[] transactionBytes = transactionCbor.HexToByteArray();

        try
        {
            using (MemoryStream stream = new MemoryStream(transactionBytes))
            {
                var retryResponse = await GetRetryPolicy<string>()
                    .ExecuteAndCaptureAsync(async () =>
                    {
                        TokenBucket.Consume(1);
                        return await TransactionsClient.PostSubmitTransactionAsync(stream)
                            .ContinueOnAnyContext();
                    }).ContinueOnAnyContext();
                var response = retryResponse.ResultOrFinalOrThrow();
                return response.ResultOrThrow();
            }
        }
        catch (ApiException ex)
        {
            throw GetHttpRequestException(ex);
        }
    }

    private HttpRequestException GetHttpRequestException(ApiException ex)
    {
        int statusCode = Convert.ToInt32(ex.StatusCode);
        if (statusCode == 418)
            statusCode = (int)HttpStatusCode.UnavailableForLegalReasons;

        string error = ex.Content ?? "";
        return new HttpRequestException(Invariant($"{ex.Message} ERROR: {error}"), ex, (HttpStatusCode)statusCode);
    }
    
    public async Task<Transaction> GetTransactionAsync(string txId)
    {
	    txId.ThrowIfNullOrEmpty();
	    Transaction response = new(txId);
	    try
	    {
		    var txRetryResponse = await GetRetryPolicy<CardanoSharp.Blockfrost.Sdk.Contracts.Transaction>()
			    .ExecuteAndCaptureAsync(async () =>
			    {
				    TokenBucket.Consume(1);
				    return await TransactionsClient.GetTransactionAsync(txId)
					    .ContinueOnAnyContext();
			    }).ContinueOnAnyContext();
		    var txResponse = txRetryResponse.ResultOrFinalOrThrow();
		    if (txResponse == null || txResponse.Content == null)
		    {
			    return response;
		    }

		    var tx = txResponse.ResultOrThrow();
		    response.BlockHash = tx.Block;
		    response.BlockHeight = tx.BlockHeight;
		    response.BlockUtcTimestamp = DateTime.UnixEpoch.AddSeconds(tx.BlockTime);
		    response.Slot = tx.Slot;
		    return response;
	    }
	    catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
	    {
		    return response;
	    }
    }
    
    public async Task<uint?> GetConfirmationsAsync(uint txBlockHeight)
    {
	    var latestBlockRetryResponse = await GetRetryPolicy<Block>()
		    .ExecuteAndCaptureAsync(async () =>
		    {
			    TokenBucket.Consume(1);
			    return await BlocksClient.GetLatestBlockAsync()
				    .ContinueOnAnyContext();
		    }).ContinueOnAnyContext();
	    
	    var latestBlockResponse = latestBlockRetryResponse.ResultOrFinalOrThrow();
	    var latestBlock = latestBlockResponse.ResultOrThrow();
	    if (latestBlock?.Height is null)
	    {
		    return null;
	    }

	    var diff = Convert.ToInt64(latestBlock.Height) - Convert.ToInt64(txBlockHeight);
	    if (diff < 0)
	    {
		    return null;
	    }

	    if (diff > uint.MaxValue)
	    {
		    return uint.MaxValue;
	    }

	    return Convert.ToUInt32(diff);
    }
}