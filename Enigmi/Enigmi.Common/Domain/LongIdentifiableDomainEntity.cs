namespace Enigmi.Common.Domain;

public abstract class LongIdentifiableDomainEntity : IdentifiableDomainEntity<long>
{
	protected LongIdentifiableDomainEntity(long id) : base(id)
	{
	}
}