using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.OrderAggregate;

namespace Enigmi.Messages.UserWallet;

public record GetActiveOrderRequest(string StakeAddress) : Request<GetActiveOrderResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetActiveOrderResponse(Guid? OrderId, OrderState? OrderState, uint? NumberOfConfirmations) : RequestResponse;
