using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;

public record RefreshStateCommand(string StakingAddress) : Command<RefreshStateResponse>, IHasWalletState
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;

    public IEnumerable<UtxoAsset> UtxoAssets { get; set; } = null!;

    public string PaymentAddress { get; set; } = null!;
}

public record RefreshStateResponse : CommandResponse;