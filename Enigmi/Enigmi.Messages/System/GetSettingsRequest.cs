using Enigmi.Common.Messaging;

namespace Enigmi.Messages.System;

public record GetSettingsRequest() : Request<GetSettingsResponse>
{
	public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Anonymous;
}

public record GetSettingsResponse(GetSettingsResponse.Settings ServerSettings) : RequestResponse
{
	public class Settings
	{
	}
}