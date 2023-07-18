using Enigmi.Common.Messaging;

namespace Enigmi.Messages.System;

public record AdminPingRequest() : Request<AdminPingResponse>
{
	public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Authorized;
}

public record AdminPingResponse() : RequestResponse
{
}