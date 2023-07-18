using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;
public record ReplyToPingUserWalletCommand(string StakeAddress) : Command<PingUserWalletResponse>
{
    public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Authorized;
}
public record PingUserWalletResponse() : CommandResponse
{
}