using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PolicyListAggregate.ValueObjects;

namespace Enigmi.Grains.Shared.PolicyCollection;

public interface IPolicyListGrain : IGrainWithIntegerKey
{
    public Task<ResultOrError<Constants.Unit>> Ping();

    Task<IEnumerable<PolicyToPuzzleCollectionMap>> GetPolicies();
}