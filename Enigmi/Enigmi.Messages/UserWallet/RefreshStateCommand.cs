using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;

public record RefreshStateCommand(string StakeAddress) : Command<RefreshStateResponse>, IHasWalletState
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;

    public IEnumerable<UtxoAsset> UtxoAssets { get; set; } = null!;
}

public record RefreshStateResponse : CommandResponse;