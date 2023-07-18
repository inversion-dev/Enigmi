using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.PolicyAggregate.Events;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.PolicyAggregate;

public class Policy : DomainEntity
{
    public Policy(string policyId, uint policyClosingSlot, DateTime policyClosingUtcDate)
    {
        Id = policyId.ThrowIfNullOrWhitespace();
        PolicyClosingSlot = policyClosingSlot;
        PolicyClosingUtcDate = policyClosingUtcDate;
        RaiseEvent(new PolicyCreated(Id));
    }

    [JsonConstructor]
    private Policy()
    {
        
    }
    
    [JsonProperty]
    public DateTime PolicyClosingUtcDate { get; private set; }

    [JsonProperty]
    public uint PolicyClosingSlot { get; private set; }

    [JsonProperty] 
    public string Id { get; private set; } = null!;
}