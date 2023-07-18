using Newtonsoft.Json;

namespace Enigmi.Common.Domain;

public abstract record DomainEvent
{
    [JsonProperty]
	public Guid Id { get; private set; } = Guid.NewGuid();
}