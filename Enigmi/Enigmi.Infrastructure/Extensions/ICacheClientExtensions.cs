using Enigmi.Common;
using Foundatio.Caching;

namespace Enigmi.Infrastructure.Extensions
{
	public static class ICacheClientExtensions
	{
		public static async Task<CacheValue<TValue>> GetOrLoadAsync<TValue>(
		this ICacheClient cacheClient,
		string key,
		Func<Task<TValue>> loaderFunc,
		TimeSpan expiresIn)
		{
			cacheClient.ThrowIfNull();
			key.ThrowIfNullOrEmpty();
			loaderFunc.ThrowIfNull();

			var cacheResponse = await cacheClient.GetAsync<TValue>(key).ContinueOnAnyContext();
			if (cacheResponse.HasValue)
			{
				return cacheResponse;
			}
			else
			{
				TValue value = await loaderFunc().ContinueOnAnyContext();
				if (value != null)
				{
					await cacheClient.SetAsync(key, value, expiresIn).ContinueOnAnyContext();
				}
				return new CacheValue<TValue>(value, true);
			}
		}
	}
}