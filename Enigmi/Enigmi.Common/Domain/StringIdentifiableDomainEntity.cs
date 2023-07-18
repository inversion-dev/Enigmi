namespace Enigmi.Common.Domain;

public abstract class StringIdentifiableDomainEntity : IdentifiableDomainEntity<string>
{
	protected StringIdentifiableDomainEntity(string id) : base(id)
	{
	}
}