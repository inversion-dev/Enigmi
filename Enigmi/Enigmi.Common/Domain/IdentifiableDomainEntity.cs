using Newtonsoft.Json;

namespace Enigmi.Common.Domain;

public abstract class IdentifiableDomainEntity<T> : DomainEntity where T : IEquatable<T>
{
	[JsonProperty]
	public T Id { get; protected set; }

	public IdentifiableDomainEntity(T id)
	{
		Id = id;
	}
}