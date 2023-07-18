using System;

namespace Enigmi.Tests;

public class ErrorMessageReturnedException : Exception
{
	public ErrorMessage ErrorMessage { get; }

	public ErrorMessageReturnedException(ErrorMessage message) : base(message.Message)
	{
		ErrorMessage =message;
	}
}