using Enigmi.Common.Messaging;

namespace Enigmi.Messages.Test;

public record RetryTestRequest() : Request<RetryTestResponse>
{
	public int ShouldTryCount { get; set; }

	public int TryCount { get; set; }
	public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Anonymous;
}

public record RetryTestResponse() : RequestResponse
{
}