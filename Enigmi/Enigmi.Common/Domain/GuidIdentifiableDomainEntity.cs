namespace Enigmi.Common.Domain;

public abstract class GuidIdentifiableDomainEntity : IdentifiableDomainEntity<Guid>
{
	protected GuidIdentifiableDomainEntity(Guid id) : base(id)
	{
	}

	protected GuidIdentifiableDomainEntity() : base(Guid.NewGuid())
	{
	}
}