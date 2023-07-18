namespace Enigmi.Infrastructure.Services
{
	public interface IPolicyVaultService
	{
		Task SetPolicyMnemonicAsync(string policyId, string mnemonic);

		Task<string> GetPolicyMnemonicAsync(string policyId);
	}
}