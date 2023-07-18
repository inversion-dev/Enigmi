using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Enigmi.Infrastructure.Extensions;
using Foundatio.Caching;
using static System.FormattableString;

namespace Enigmi.Infrastructure.Services
{
	public abstract class KeyVaultService
	{
		protected ICacheClient CacheClient { get; }

		protected abstract SecretClient SecretClient { get; }

		protected static readonly SecretClientOptions SecretClientOptions = new SecretClientOptions()
		{
			Retry = {
				Delay= TimeSpan.FromSeconds(2),
				MaxDelay = TimeSpan.FromSeconds(16),
				MaxRetries = 5,
				Mode = RetryMode.Exponential,
			}
		};

		public KeyVaultService(ICacheClient cacheClient)
		{
			CacheClient = cacheClient.ThrowIfNull();
		}

		protected async Task<string> GetStringAsync(string name)
		{
			name.ThrowIfNullOrWhitespace();
			var cachedValue = await CacheClient.GetOrLoadAsync(
				GetMethodCacheKey(name),
				async () => { return await GetValueAsync(name).ContinueOnAnyContext(); },
				TimeSpan.FromMinutes(5)).ContinueOnAnyContext();
			return cachedValue.Value;
		}

		protected async Task<string> GetValueAsync(string name)
		{
			name.ThrowIfNullOrWhitespace();
			string keyvaultName = GetKeyVaultName(name);

			var result = await SecretClient.GetSecretAsync(keyvaultName).ContinueOnAnyContext();
			var raw = result.GetRawResponse();

			if (raw.Status != 200)
				throw new Common.Exceptions.ApplicationException($"Could not get value for '{name}' from KeyVault '{keyvaultName}': {raw.Status} {raw.ReasonPhrase}");

			return result.Value.Value;
		}

		private string GetKeyVaultName(string name)
		{
			return name.Replace("_", "", StringComparison.InvariantCulture);
		}

		protected string GetMethodCacheKey(string name, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
		{
			return Invariant($"{this.GetType().Name}_{memberName}_{name}");
		}
	}
}