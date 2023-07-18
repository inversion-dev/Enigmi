using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.Order.Messages;
using Enigmi.Grains.Shared.UserWallet.Messages;

namespace Enigmi.Grains.Shared.Order;

public interface IOrderGrain : IGrainWithGuidKey
{
    Task<ResultOrError<BuildOrderResponse>> BuildOrder(BuildOrderCommand buildOrderCommand);

    Task<ResultOrError<ApproveOrderResponse>> ApproveOrder(ApproveOrderCommand approveOrderCommand);

    ValueTask<Domain.Entities.OrderAggregate.Order> GetOrder();

    Task<ResultOrError<CancelOrderResponse>> CancelOrder(CancelOrderCommand command);

    Task ProcessCancelOrder();
}