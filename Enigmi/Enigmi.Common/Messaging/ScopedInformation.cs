using System.Collections.ObjectModel;
using Enigmi.Common.Domain;
using System.Security.Claims;

namespace Enigmi.Common.Messaging;

public class ScopedInformation
{
	private const string MessageNameHeaderKey = "MessageName";

	public ClaimsPrincipal? ClaimsPrincipal { get; private set; }

	public string? MessageName { get; private set; }

	public string UserFullName { get; private set; } = "Not Set";

	public UserContext UserContext { get; private set; } = null!;
	
	public ReadOnlyDictionary<string, IEnumerable<string?>>? Headers { get; private set; }

	public ScopedInformation AsSystemUser()
	{
		UserFullName = "System";
		return this;
	}

	public ScopedInformation AsAnonymousUser()
	{
		UserFullName = "Anonymous";
		return this;
	}

	public ScopedInformation WithClaimsPrincipal(ClaimsPrincipal claimPrincipal)
	{
		ClaimsPrincipal = claimPrincipal;
		return this;
	}

	public ScopedInformation WithMessageName<T>() where T : IMessage
	{
		MessageName = typeof(T).FullName;

		return this;
	}

	public ScopedInformation WithMessageName(object message)
	{
		message.ThrowIfNull();

		MessageName = message.GetType().FullName;

		return this;
	}

	public ScopedInformation WithUserContext(UserContext userContext)
	{
		UserContext = userContext.ThrowIfNull();
		return this;
	}

	public void WithHeaders(ReadOnlyDictionary<string, IEnumerable<string?>> headers)
	{
		headers.ThrowIfNull();
		this.Headers = headers;
	}

	public void CopyFrom(ScopedInformation scopedInformation)
	{
		var properties = typeof (ScopedInformation).GetProperties().Where(x => x.CanRead).ToList();
		foreach (var property in properties)
		{
			if(property.CanWrite) 
			{
				property.SetValue(this, property.GetValue(scopedInformation, null), null);
			}
		}
	}
}