using Enigmi.Common.Messaging;

namespace Enigmi.Messages.System;

public record RunAdminActionCommand(string Action, string? AdditionalData) : Command<RunAdminActionResponse>
{
	public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Authorized;
}

public record RunAdminActionResponse(string Message) : CommandResponse;