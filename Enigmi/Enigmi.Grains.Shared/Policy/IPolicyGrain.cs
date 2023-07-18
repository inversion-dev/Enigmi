using Enigmi.Common.Messaging;

namespace Enigmi.Grains.Shared.Policy;

public interface IPolicyGrain : IGrainWithStringKey
{
    Task<ResultOrError<Constants.Unit>> CreatePolicy(string policyIdString, string mnemonicWords, uint policyClosingSlot, DateTime policyClosingUtcDate);

    Task<ResultOrError<string>> GetMnemonic();

    Task<Domain.Entities.PolicyAggregate.Policy> GetPolicy();
}