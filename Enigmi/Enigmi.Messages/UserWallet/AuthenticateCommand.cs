using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;

public record AuthenticateCommand(string AddressHex, string Payload, string KeyHex, string SignatureHex) : Command<AuthenticateResponse>
{
    public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Anonymous;
}

public record AuthenticateResponse(string? Token) : CommandResponse;