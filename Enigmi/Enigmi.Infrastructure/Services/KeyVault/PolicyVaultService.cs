using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Enigmi.Common;
using Foundatio.Caching;
using static Enigmi.Common.Settings;

namespace Enigmi.Infrastructure.Services
{
	public sealed class PolicyVaultService : KeyVaultService, IPolicyVaultService
	{
		private readonly PolicyVaultSettings SystemWalletSettings;

		protected override SecretClient SecretClient { get; }

		public PolicyVaultService(
			PolicyVaultSettings systemWalletSettings,
			ICacheClient cacheClient)
				: base(cacheClient)
		{
			SystemWalletSettings = systemWalletSettings.ThrowIfNull();

			SecretClient = new SecretClient(new Uri(SystemWalletSettings.PolicyVaultUrl), new DefaultAzureCredential(), SecretClientOptions);
		}


		public async Task SetPolicyMnemonicAsync(string policyId, string mnemonic)
		{
			await SecretClient.SetSecretAsync(policyId.ThrowIfNullOrWhitespace(), mnemonic.ThrowIfNullOrWhitespace()).ContinueOnAnyContext();
		}
		
		public async Task<string> GetPolicyMnemonicAsync(string policyId)
		{
			return await this.GetStringAsync(policyId.ThrowIfNullOrWhitespace()).ContinueOnAnyContext();
		}
	}
}