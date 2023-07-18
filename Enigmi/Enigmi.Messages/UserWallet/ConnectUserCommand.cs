using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;
public record ConnectUserCommand(string StakeAddress) : Command<UserConnectedResponse>, IHasWalletState
{
    public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Authorized;
    public IEnumerable<UtxoAsset> UtxoAssets { get; set; }= null!;
}
public record UserConnectedResponse : CommandResponse
{
}