namespace Enigmi.Common.Domain;

public abstract class UserContext
{
	public UserContext(string fullName)
	{
		FullName = fullName.ThrowIfNullOrWhitespace();
	}

	public string FullName { get; private set; }
}

public sealed class AnonymousUserContext : UserContext
{
	public AnonymousUserContext(string? fullName) : base(fullName ?? "Anonymous")
	{
	}
}

public sealed class AuthorizedUserContext : UserContext
{
	public AuthorizedUserContext(string fullName)
		: base(fullName)
	{
	}
}