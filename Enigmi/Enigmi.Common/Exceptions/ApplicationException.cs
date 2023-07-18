namespace Enigmi.Common.Exceptions;

public class ApplicationException : Exception
{
	public string? UserFriendlyMessage { get; }

	public bool LogAsWarning { get; }

	public ApplicationException()
	{
	}

	public ApplicationException(string message) : base(message)
	{
	}

	public ApplicationException(string message, string userFriendlyMessage)
		: base(message)
	{
		UserFriendlyMessage = userFriendlyMessage;
	}
	
	public ApplicationException(string message, string userFriendlyMessage, bool logAsWarning)
		: base(message)
	{
		UserFriendlyMessage = userFriendlyMessage;
		LogAsWarning = logAsWarning;
	}

	public ApplicationException(string message, Exception inner) : base(message, inner)
	{
	}

	public ApplicationException(string message, Exception inner, string userFriendlyMessage)
		: base(message, inner)
	{
		UserFriendlyMessage = userFriendlyMessage;
	}

	public ApplicationException(string message, Exception inner, string userFriendlyMessage, bool logAsWarning)
	: base(message, inner)
	{
		UserFriendlyMessage = userFriendlyMessage;
		LogAsWarning = logAsWarning;
	}
}