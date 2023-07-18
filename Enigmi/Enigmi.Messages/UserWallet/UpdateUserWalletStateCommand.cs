using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;
public record UpdateUserWalletStateCommand(string StakeAddress) : Command<SendWalletUtxosResponse>, IHasWalletState
{
    public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Authorized;
    public IEnumerable<UtxoAsset> UtxoAssets { get; set; } = null!;
}
public record SendWalletUtxosResponse() : CommandResponse
{
}