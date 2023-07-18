using Enigmi.Common;

public class ErrorMessage
{
	public ErrorMessage(string error, string message)
	{
		Error = error.ThrowIfNullOrWhitespace();
		Message = message.ThrowIfNullOrWhitespace();
	}

	public string Error { get; }

	public string Message { get; }
}