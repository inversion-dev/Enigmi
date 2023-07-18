using CardanoSharp.Wallet.CIPs.CIP14.Extensions;
using Domain.ValueObjects;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Common.Utils;
using Enigmi.Domain.Entities.OrderAggregate;
using Enigmi.Domain.Entities.OrderAggregate.Events;
using Enigmi.Domain.Entities.PolicyListAggregate;
using Enigmi.Domain.Entities.PolicyListAggregate.ValueObjects;
using Enigmi.Domain.Entities.PuzzleDefinitionAggregate;
using Enigmi.Domain.Entities.PuzzlePieceAggregate;
using Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate.Events;
using Enigmi.Domain.Utils;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;
using Enigmi.Grains.Shared.Order;
using Enigmi.Grains.Shared.Order.Messages;
using Enigmi.Grains.Shared.PolicyCollection;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.PuzzlePieceDispenser;
using Enigmi.Grains.Shared.PuzzlePieceDispenser.Messages;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Infrastructure.Extensions;
using Enigmi.Infrastructure.Services.SignalR;
using Enigmi.Messages.SignalRMessage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Orleans.Providers;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;
using ConnectUserCommand = Enigmi.Grains.Shared.UserWallet.Messages.ConnectUserCommand;
using UpdateUserWalletStateCommand = Enigmi.Grains.Shared.UserWallet.Messages.UpdateUserWalletStateCommand;

namespace Enigmi.Application.UserWallet;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public partial class UserWalletGrain : GrainBase<Domain.Entities.UserWalletAggregate.UserWallet>, IUserWalletGrain
{
    private Settings Settings { get; }

    private ILogger<UserWalletGrain> Logger { get; }

    private ISignalRHubContextStore SignalRHubContextStore { get; }

    public UserWalletGrain(ILogger<UserWalletGrain> logger, 
        ISignalRHubContextStore signalRHubContextStore,
        Settings settings)
    {
        Settings = settings.ThrowIfNull();
        Logger = logger.ThrowIfNull();
        SignalRHubContextStore = signalRHubContextStore.ThrowIfNull();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.DomainAggregate == null)
        {
            State.DomainAggregate = new Domain.Entities.UserWalletAggregate.UserWallet();
            await WriteStateAsync();
        }

        if (State.DomainAggregate.OnlineState == OnlineState.Online)
        {
            await PingUser();
        }

        await Subscribe<UserWalletWentOffline>(this.GetPrimaryKeyString(), OnUserWalletWentOffline);
        await Subscribe<OrderCancelled>(this.GetPrimaryKeyString(), OnOrderCancelled);

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task OnOrderCancelled(OrderCancelled @event)
    {
        if (State.DomainAggregate == null)
        {
            return;
        }

        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(@event.OrderId);
        var order = await orderGrain.GetOrder();

        if (order.OrderPuzzleCollectionId == null)
        {
            return;
        }

        Logger.LogInformation(Invariant($"UserWalletGrain: OnOrderCancelled - {@event.OrderId}"));

        var dispenserGrain = GrainFactory.GetGrain<IPuzzlePieceDispenserGrain>(PuzzlePieceDispenser.GetId(order.OrderPuzzleCollectionId!.Value, order.OrderPuzzleSize!.Value));
        await dispenserGrain.Release(@event.OrderId);
        State.DomainAggregate.OnCancelOrder(@event.OrderId);

        await WriteStateAsync();
    }

    private async Task OnUserWalletWentOffline(UserWalletWentOffline @event)
    {
        State.DomainAggregate.ThrowIfNull();
        if (State.DomainAggregate.OnlineState == OnlineState.Offline)
        {
            var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(0);
            await activePuzzlePieceListGrain.UpdateActivePuzzlePieces(new UpdateActivePuzzlePiecesCommand(this.GetPrimaryKeyString(),new List<UpdateActivePuzzlePiecesCommand.PuzzlePiece>()));
            
            await SendSignalRMessage(new NotifyUserAboutOfflineState());    
        }
    }

    private IClientProxy UserSignalRChannel()
    {
        return SignalRHubContextStore.MessageHubContext!.Clients.Users(new[] { this.GetPrimaryKeyString() });
    }

    public async Task<ResultOrError<CreateOrderResponse>> CreateOrder(CreateOrderCommand command)
    {
        command.ThrowIfNull();

        string? warning = null;
        if (State.DomainAggregate == null)
            throw new Exception("UserWallet does not exist yet");

        if (State.DomainAggregate.ActiveOrderId != Guid.Empty)
        {
            var previousOrderGrain = GrainFactory.GetGrain<IOrderGrain>(State.DomainAggregate.ActiveOrderId);
            var previousOrder = await previousOrderGrain.GetOrder();
            if (previousOrder.IsOrderCancellationAllowed)
            {
                Logger.LogInformation(Invariant($"UserWalletGrain: Cancelling Order - {State.DomainAggregate.ActiveOrderId}"));
                await previousOrderGrain.CancelOrder(new CancelOrderCommand());
            }
            else
            {
                if (previousOrder.State is OrderState.TransactionSigned
                    or OrderState.TransactionSubmitted)
                {
                    return "Order is in progress".ToFailedResponse<CreateOrderResponse>();
                }
            }
        }

        var dispenserGrain = GrainFactory.GetGrain<IPuzzlePieceDispenserGrain>(PuzzlePieceDispenser.GetId(command.PuzzleCollectionId, command.PuzzleSize));

        var orderId = Guid.NewGuid();
        var reservationResponse = await dispenserGrain.ReserveRandomPuzzlePieces(new ReserveRandomPuzzlePiecesCommand(orderId, command.Quantity));
        if (reservationResponse.HasErrors)
        {
            return reservationResponse.Errors.ToFailedResponse<CreateOrderResponse>();
        }

        var reservationResponseResult = reservationResponse.Result.ThrowIfNull();
        if (reservationResponseResult.DispensedPuzzlePieceIds.Count == 0)
        {
            return "No puzzle piece(s) are available to reserve".ToFailedResponse<CreateOrderResponse>();
        }

        if (reservationResponseResult.DispensedPuzzlePieceIds.Count < command.Quantity)
        {
            warning = "Less items has been reserved than requested";
        }

        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(orderId);
        var buildOrderCommand = new BuildOrderCommand(
            command.PaymentAddress,
            this.GetPrimaryKeyString(),
            command.PuzzleCollectionId,
            command.PuzzleSize,
            reservationResponseResult.DispensedPuzzlePieceIds.ToList(),
            State.DomainAggregate.AvailableUtxoAssets.ToList());

        var buildOrderResponse = await orderGrain.BuildOrder(buildOrderCommand);
        if (buildOrderResponse.HasErrors)
        {
            return buildOrderResponse.Errors.ToFailedResponse<CreateOrderResponse>();
        }

        var response = buildOrderResponse.Result.ThrowIfNull();
        State.DomainAggregate!.SetActiveOrder(orderId, response.UsedUtxos);
        Logger.LogInformation(Invariant($"UserWalletGrain: Active Order - {orderId}"));
        await WriteStateAsync();

        return new CreateOrderResponse(orderId, response.UnsignedTransactionCborHex, response.Fee, warning).ToSuccessResponse();
    }

    public async Task<ResultOrError<GetActiveOrderResponse>> GetActiveOrder()
    {
        if (State.DomainAggregate == null || State.DomainAggregate.ActiveOrderId == Guid.Empty)
        {
            return new GetActiveOrderResponse(null, null, null).ToSuccessResponse();
        }

        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(State.DomainAggregate.ActiveOrderId);
        var orderDetail = await orderGrain.GetOrder();

        if (orderDetail.State is OrderState.Completed
            or OrderState.Cancelled)
        {
            return new GetActiveOrderResponse(null, null, null).ToSuccessResponse();
        }
        
        return new GetActiveOrderResponse(orderDetail.Id, orderDetail.State, orderDetail.NumberOfConfirmations).ToSuccessResponse();
    }

    public async Task<ResultOrError<ApproveOrderResponse>> ApproveOrder(ApproveOrderCommand approvedOrderCommand)
    {
        approvedOrderCommand.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        var order = State.DomainAggregate;
        if (order.ActiveOrderId != approvedOrderCommand.OrderId)
        {
            return "Active order does not match the approved order".ToFailedResponse<ApproveOrderResponse>();
        }

        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(State.DomainAggregate.ActiveOrderId);
        var orderDetails = await orderGrain.GetOrder();

        var puzzlePieceDispenserGrain = GrainFactory.GetGrain<IPuzzlePieceDispenserGrain>(PuzzlePieceDispenser.GetId(orderDetails.OrderPuzzleCollectionId!.Value, orderDetails.OrderPuzzleSize!.Value));
        var commitReservationResponse = await puzzlePieceDispenserGrain.CommitReservation(approvedOrderCommand.OrderId);
        if (commitReservationResponse.HasErrors)
        {
            return commitReservationResponse.Transform(o => new ApproveOrderResponse());
        }

        var approveOrderResponse = await orderGrain.ApproveOrder(approvedOrderCommand);
        return approveOrderResponse.Transform(o => new ApproveOrderResponse());
    }

    public async Task<ResultOrError<Constants.Unit>> SendSignalRMessage(ISignalRMessage message)
    {   
        await UserSignalRChannel().SendAsync(message);
        return new Constants.Unit().ToSuccessResponse();
    }

    public async Task<ResultOrError<UpdateUserWalletStateResponse>> UpdateWalletState(UpdateUserWalletStateCommand command)
    {
        command.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.UpdateWalletState(command.Utxos);
        await WriteStateAsync();
        
        var updateActivePuzzlePiecesResponse = await UpdateActivePuzzlePieces();
        if (updateActivePuzzlePiecesResponse.HasErrors)
        {
            return updateActivePuzzlePiecesResponse.Errors.ToFailedResponse<UpdateUserWalletStateResponse>();
        }

        return new UpdateUserWalletStateResponse().ToSuccessResponse();
    }

    private async Task<ResultOrError<Constants.Unit>> UpdateActivePuzzlePieces()
    {
        State.DomainAggregate.ThrowIfNull();
        var tokenAssets = State.DomainAggregate.AvailableUtxoAssets
            .Where(x => x.BlockchainAssetId != Constants.LovelaceTokenAssetId).ToList();
        
        var puzzlePiecesPolicies = await GetPuzzlePiecesPolicies();

        var ownedPuzzlePiecesIds = GetOwnedPuzzlePiecesIds(tokenAssets, puzzlePiecesPolicies);

        var puzzlePieces = await BuildActivePuzzlePiecesList(ownedPuzzlePiecesIds);

        var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(0);
        var updateActivePuzzlePiecesResponse = await activePuzzlePieceListGrain.UpdateActivePuzzlePieces(new UpdateActivePuzzlePiecesCommand(this.GetPrimaryKeyString(),puzzlePieces));

        if (updateActivePuzzlePiecesResponse.HasErrors)
        {
            return updateActivePuzzlePiecesResponse.Errors.ToFailedResponse<Constants.Unit>();
        }

        return new Constants.Unit().ToSuccessResponse();
    }

    private async Task<List<PolicyToPuzzleCollectionMap>> GetPuzzlePiecesPolicies()
    {
        var policyCollectionGrain = this.GrainFactory.GetGrain<IPolicyListGrain>(0);
        var policies = await policyCollectionGrain.GetPolicies();
        var puzzlePiecesPolicies = policies.Where(x => x.PolicyType == PolicyType.PuzzlePiece).ToList();
        return puzzlePiecesPolicies;
    }

    private async Task<List<UpdateActivePuzzlePiecesCommand.PuzzlePiece>> BuildActivePuzzlePiecesList(List<string> ownedPuzzlePiecesIds)
    {
        var puzzlePieces = new List<UpdateActivePuzzlePiecesCommand.PuzzlePiece>();
        foreach (var puzzlePieceId in ownedPuzzlePiecesIds)
        {
            var puzzlePieceGrain = GrainFactory.GetGrain<IPuzzlePieceGrain>(puzzlePieceId);

            var puzzlePiece = await puzzlePieceGrain.GetPuzzlePiece();
            if (puzzlePiece == null)
            {
                throw new ApplicationException($"Could not get puzzle piece detail for puzzlePieceId {puzzlePieceId}");
            }

            puzzlePieces.Add(new UpdateActivePuzzlePiecesCommand.PuzzlePiece(puzzlePieceId, puzzlePiece.PuzzlePieceDefinitionId, puzzlePiece.PuzzleDefinitionId));
        }

        return puzzlePieces;
    }

    public async Task<ResultOrError<ConnectUserResponse>> Connect(ConnectUserCommand command)
    {
        command.ThrowIfNull();
        await UpdateWalletState(new UpdateUserWalletStateCommand(command.UtxoAssets));
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.GoOnline();
        await PingUser();
        await ReschedulePingTimerIfRequired();

        return new ConnectUserResponse().ToSuccessResponse();
    }

    public Task<Domain.Entities.UserWalletAggregate.UserWallet> GetUserWallet()
    {
        State.DomainAggregate.ThrowIfNull();
        return Task.FromResult(State.DomainAggregate);
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        string subscriptionName = @event switch
        {
            UserWalletWentOffline => this.GetPrimaryKeyString(),
            _ => string.Empty,
        };

        return subscriptionName;
    }
    
    public async Task<GetStateResponse> GetState()
    {
        State.DomainAggregate.ThrowIfNull();
        var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(0);
        return await activePuzzlePieceListGrain.GetActivePuzzlePieces(this.GetPrimaryKeyString());
    }

    public async Task<GetStateResponse?> GetActiveCompletedOrderPuzzlePieces(Guid orderId)
    {
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ActiveOrderId.ThrowIfNull();
        orderId.ThrowIfEmpty();

        if (State.DomainAggregate.ActiveOrderId != orderId)
        {
            throw new ApplicationException("Requested orderId does not match user wallet active orderId");
        }

        var orderGrain = GrainFactory.GetGrain<IOrderGrain>(State.DomainAggregate.ActiveOrderId);
        var order = await orderGrain.GetOrder();
        if (order.State != OrderState.Completed)
        {
            throw new ApplicationException("Order is not in completed state");
        }
        
        var puzzlePieceIds = order.OrderedPuzzlePieces.Select(x => x.Id).ToList();

        var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(0);
        var puzzlePiecesIncludingNotOwned = await activePuzzlePieceListGrain.GetActivePuzzlePieces(puzzlePieceIds, this.GetPrimaryKeyString());
        var response = puzzlePiecesIncludingNotOwned with { PuzzlePieces = puzzlePiecesIncludingNotOwned.PuzzlePieces.Where(x => x.IsOwned).ToList() };
        
        return response;
    }

    public async Task UserWalletStateHasChanged()
    {
        await UserSignalRChannel().SendAsync(new UserWalletStateHasChanged());
    }

    private List<string> GetOwnedPuzzlePiecesIds(List<UtxoAsset> tokenAssets, List<PolicyToPuzzleCollectionMap> puzzlePiecesPolicies)
    {
        var puzzlePieceIds = new List<string>();
        
        foreach (var token in tokenAssets)
        {
            (byte[] policyId, byte[] assetName) =
                CardanoHelper.ConvertAssetIdToPolicyIdAndAssetName(token.BlockchainAssetId);
            if (puzzlePiecesPolicies.Any(x =>
                    string.Equals(x.PolicyId, Convert.ToHexString(policyId), StringComparison.InvariantCultureIgnoreCase)))
            {
                puzzlePieceIds.Add(AssetExtensions.ToAssetFingerprint(CardanoHelper.GetAssetId(policyId, assetName)));
            }
        }

        return puzzlePieceIds;
    }
}