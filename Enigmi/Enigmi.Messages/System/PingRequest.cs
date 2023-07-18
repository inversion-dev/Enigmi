using Enigmi.Common.Messaging;

namespace Enigmi.Messages.System;

public record PingRequest() : Request<PingResponse>
{
	public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Anonymous;
}

public record PingResponse() : RequestResponse
{
}