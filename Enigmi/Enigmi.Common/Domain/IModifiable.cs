namespace Enigmi.Common.Domain;

public interface IModifiable
{
	string LastModifiedByFullName { get; }

	DateTime LastModifiedUtcTimestamp { get; }

	public void SetModifiedBy(string fullName, DateTime timeStamp);
}