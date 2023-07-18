using Enigmi.Common;
using Esendex.TokenBucket;
using static Enigmi.Common.Settings;

namespace Enigmi.Infrastructure.Services.BlockchainService;

public class BlockfrostTokenBucket : ITokenBucket
{
	private ITokenBucket bucket;

	public BlockfrostTokenBucket(BlockfrostSettings config)
	{
		config.ThrowIfNull();
		bucket = TokenBuckets.Construct()
		.WithCapacity(config.BurstRequestLimit)
		.WithFixedIntervalRefillStrategy(config.RequestsPerSecond, TimeSpan.FromSeconds(1))
		.Build();
	}

	public bool TryConsume()
	{
		return bucket.TryConsume();
	}

	public bool TryConsume(long numTokens)
	{
		return bucket.TryConsume(numTokens);
	}

	public void Consume()
	{
		bucket.Consume();
	}

	public void Consume(long numTokens)
	{
		bucket.Consume(numTokens);
	}
}