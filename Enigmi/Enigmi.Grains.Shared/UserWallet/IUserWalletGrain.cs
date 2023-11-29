using Domain.ValueObjects;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Orleans.Concurrency;

namespace Enigmi.Grains.Shared.UserWallet;

public interface IUserWalletGrain : IGrainWithStringKey
{
    Task<ResultOrError<CreateOrderResponse>> CreateOrder(CreateOrderCommand command);

    Task<ResultOrError<GetActiveOrderResponse>> GetActiveOrder();
    
    Task<ResultOrError<Constants.Unit>> ReplyToClientPing();
    
    Task<ResultOrError<UpdateUserWalletStateResponse>> UpdateWalletState(UpdateUserWalletStateCommand command);
    
    Task<ResultOrError<ConnectUserResponse>> Connect(ConnectUserCommand command);

    Task<Domain.Entities.UserWalletAggregate.UserWallet> GetUserWallet();
        
    Task<ResultOrError<ApproveOrderResponse>> ApproveOrder(ApproveOrderCommand approvedOrderCommand);

    [AlwaysInterleave]
    Task<ResultOrError<Constants.Unit>> SendSignalRMessage(ISignalRMessage message);

    Task PingUser();
    
    Task<GetStateResponse> GetState();
    
    Task<GetStateResponse?> GetActiveCompletedOrderPuzzlePieces(Guid orderId);

    Task UserWalletStateHasChanged();

    Task<ResultOrError<MakeAnOfferResponse>> MakeAnOffer(MakeAnOfferCommand command);
    
    Task<ResultOrError<GetTradeResponse>> GetActiveTradeList(GetActiveTradeListRequest request);

    Task<ResultOrError<Constants.Unit>> ReserveUtxos(IEnumerable<Utxo> utxosToReserve, IEnumerable<string> reservedAssetFingerprints, string reservedBy);

    Task<ResultOrError<Constants.Unit>> ReleaseUtxoReservations(string reservedBy);

    Task<ResultOrError<bool>> DoesUtxoReservationsExist(string reservedBy);
}