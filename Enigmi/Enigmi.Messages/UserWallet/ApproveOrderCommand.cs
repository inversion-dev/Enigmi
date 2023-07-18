using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;

public record ApproveOrderCommand(string StakeAddress, Guid OrderId, string WitnessCbor)  : Command<ApproveOrderResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record ApproveOrderResponse() : CommandResponse;



