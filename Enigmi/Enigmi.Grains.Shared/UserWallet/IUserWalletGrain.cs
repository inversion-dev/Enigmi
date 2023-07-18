using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Orleans.Concurrency;

namespace Enigmi.Grains.Shared.UserWallet;

public interface IUserWalletGrain : IGrainWithStringKey
{
    Task<ResultOrError<CreateOrderResponse>> CreateOrder(CreateOrderCommand command);

    Task<ResultOrError<GetActiveOrderResponse>> GetActiveOrder();

    [AlwaysInterleave]
    Task<ResultOrError<Constants.Unit>> ReplyToClientPing();

    [AlwaysInterleave]
    Task<ResultOrError<UpdateUserWalletStateResponse>> UpdateWalletState(UpdateUserWalletStateCommand command);

    [AlwaysInterleave]
    Task<ResultOrError<ConnectUserResponse>> Connect(ConnectUserCommand command);

    Task<Domain.Entities.UserWalletAggregate.UserWallet> GetUserWallet();

    [AlwaysInterleave]
    Task<ResultOrError<ApproveOrderResponse>> ApproveOrder(ApproveOrderCommand approvedOrderCommand);

    [AlwaysInterleave]
    Task<ResultOrError<Constants.Unit>> SendSignalRMessage(ISignalRMessage message);

    Task PingUser();
    
    Task<GetStateResponse> GetState();
    
    Task<GetStateResponse?> GetActiveCompletedOrderPuzzlePieces(Guid orderId);

    Task UserWalletStateHasChanged();
}