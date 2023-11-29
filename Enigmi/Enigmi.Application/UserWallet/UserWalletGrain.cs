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
using Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate;
using Enigmi.Domain.Entities.TradeAggregate.Events;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate.Events;
using Enigmi.Domain.Utils;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;
using Enigmi.Grains.Shared.ActiveUtxoReservationsList;
using Enigmi.Grains.Shared.Order;
using Enigmi.Grains.Shared.Order.Messages;
using Enigmi.Grains.Shared.PolicyCollection;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.PuzzlePieceDispenser;
using Enigmi.Grains.Shared.PuzzlePieceDispenser.Messages;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Grains.Shared.Trade.Messages;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Grains.Shared.UserWalletActiveTradeList;
using Enigmi.Infrastructure.Extensions;
using Enigmi.Infrastructure.Services.SignalR;
using Enigmi.Messages.SignalRMessage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Orleans.Providers;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;
using ConnectUserCommand = Enigmi.Grains.Shared.UserWallet.Messages.ConnectUserCommand;
using OrderCompleted = Enigmi.Domain.Entities.OrderAggregate.Events.OrderCompleted;
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
            State.DomainAggregate = new Domain.Entities.UserWalletAggregate.UserWallet(this.GetPrimaryKeyString());
            await WriteStateAsync();
        }

        if (State.DomainAggregate.OnlineState == OnlineState.Online)
        {
            await PingUser();
        }

        await Subscribe<UserWalletWentOffline>(this.GetPrimaryKeyString(), OnUserWalletWentOffline);
        await Subscribe<OrderCancelled>(this.GetPrimaryKeyString(), OnOrderCancelled);
        await Subscribe<OrderCompleted>(this.GetPrimaryKeyString(), OnOrderCompleted);
        await Subscribe<OrderSubmissionFailed>(this.GetPrimaryKeyString(), OnOrderSubmissionFailed);

        await Subscribe<TradeCancelled>(this.GetPrimaryKeyString(), OnTradeCancelled);
        await Subscribe<TradeCompleted>(this.GetPrimaryKeyString(), OnTradeCompleted);
        await Subscribe<TradeBlockchainSubmissionFailed>(this.GetPrimaryKeyString(), OnTradeBlockchainSubmissionFailed);

        var userWalletActiveTradeListGrain = GrainFactory.GetGrain<IUserWalletActiveTradeListGrain>(State.DomainAggregate.StakingAddress);        
        await userWalletActiveTradeListGrain.Create();

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task OnTradeBlockchainSubmissionFailed(TradeBlockchainSubmissionFailed @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReleaseUtxoReservations(State.DomainAggregate.GetReserveByKey(Reserver.Trade, @event.TradeId));
        await WriteStateAsync();
    }

    private async Task OnTradeCompleted(TradeCompleted @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReleaseUtxoReservations(State.DomainAggregate.GetReserveByKey(Reserver.Trade, @event.TradeId));
        await WriteStateAsync();
    }

    private async Task OnTradeCancelled(TradeCancelled @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReleaseUtxoReservations(State.DomainAggregate.GetReserveByKey(Reserver.Trade, @event.TradeId));
        await WriteStateAsync();
    }

    private async Task OnOrderCompleted(OrderCompleted @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReleaseUtxoReservations(State.DomainAggregate.GetReserveByKey(Reserver.Order, @event.OrderId));
        await WriteStateAsync();
    }
    
    private async Task OnOrderSubmissionFailed(OrderSubmissionFailed @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReleaseUtxoReservations(State.DomainAggregate.GetReserveByKey(Reserver.Order, @event.OrderId));
        await WriteStateAsync();
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
        State.DomainAggregate.ReleaseUtxoReservations(State.DomainAggregate.GetReserveByKey(Reserver.Order, @event.OrderId));

        await WriteStateAsync();
    }

    private async Task OnUserWalletWentOffline(UserWalletWentOffline @event)
    {
        State.DomainAggregate.ThrowIfNull();
        if (State.DomainAggregate.OnlineState == OnlineState.Offline)
        {
            var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var nickname = State.DomainAggregate.Nickname;
            await activePuzzlePieceListGrain.UpdateActivePuzzlePieces(new UpdateActivePuzzlePiecesCommand(this.GetPrimaryKeyString(), nickname, new List<UpdateActivePuzzlePiecesCommand.PuzzlePiece>()));

            var userWalletActiveTradeListGrain = this.GrainFactory.GetGrain<IUserWalletActiveTradeListGrain>(this.GetPrimaryKeyString());
            var trades = await userWalletActiveTradeListGrain.GetActiveTrades();
            foreach (var trade in trades)
            {
                var tradeGrain = this.GrainFactory.GetGrain<ITradeGrain>(trade.Id);
                await tradeGrain.GoOffline(this.GetPrimaryKeyString());
            }
            
            await SendSignalRMessage(new NotifyUserAboutOfflineState());    
        }
    }

    private IClientProxy UserSignalRChannel()
    {
        return SignalRHubContextStore.MessageHubContext!.Clients.Users(new[] { this.GetPrimaryKeyString() });
    }

    public async Task<ResultOrError<Constants.Unit>> ReserveUtxos(IEnumerable<Utxo> utxosToReserve, IEnumerable<string> reservedAssetFingerprints, string reservedBy)
    {
        var activeUtxoReservationsListGrain = GrainFactory.GetGrain<IActiveUtxoReservationsListGrain>(Constants.ActiveUtxoReservationsListGrainKey);
        await activeUtxoReservationsListGrain.Initialize();
        
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReserveUtxos(utxosToReserve, reservedAssetFingerprints, reservedBy);
        
        await WriteStateAsync();
        return new Constants.Unit().ToSuccessResponse();
    }

    public async Task<ResultOrError<Constants.Unit>> ReleaseUtxoReservations(string reservedBy)
    {
        var activeUtxoReservationsListGrain = GrainFactory.GetGrain<IActiveUtxoReservationsListGrain>(Constants.ActiveUtxoReservationsListGrainKey);
        await activeUtxoReservationsListGrain.Initialize();
        
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.ReleaseUtxoReservations(reservedBy);
        
        await WriteStateAsync();
        return new Constants.Unit().ToSuccessResponse();
    }
    
    public Task<ResultOrError<bool>> DoesUtxoReservationsExist(string reservedBy)
    {
        State.DomainAggregate.ThrowIfNull();
        var exists = State.DomainAggregate.DoesReservationExist(reservedBy);
        return Task.FromResult(exists.ToSuccessResponse());
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
        var reservationResponse = await dispenserGrain.ReservePuzzlePieces(new ReservePuzzlePiecesCommand(orderId, command.Quantity));
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
            State.DomainAggregate.UnreservedUtxoAssets.ToList());

        var buildOrderResponse = await orderGrain.BuildOrder(buildOrderCommand);
        if (buildOrderResponse.HasErrors)
        {
            return buildOrderResponse.Errors.ToFailedResponse<CreateOrderResponse>();
        }
        
        var response = buildOrderResponse.Result.ThrowIfNull();
        State.DomainAggregate!.SetActiveOrder(orderId, response.UsedUtxos, new List<string>());
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

        if (orderDetail.State is OrderState.Completed or OrderState.Cancelled)
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
        State.DomainAggregate.UpdateWalletState(command.Utxos, command.PaymentAddress);
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

        var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
        var nickname = State.DomainAggregate.Nickname;
        var updateActivePuzzlePiecesResponse = await activePuzzlePieceListGrain.UpdateActivePuzzlePieces(new UpdateActivePuzzlePiecesCommand(this.GetPrimaryKeyString(),nickname, puzzlePieces));

        if (updateActivePuzzlePiecesResponse.HasErrors)
        {
            return updateActivePuzzlePiecesResponse.Errors.ToFailedResponse<Constants.Unit>();
        }

        return new Constants.Unit().ToSuccessResponse();
    }

    private async Task<List<PolicyToPuzzleCollectionMap>> GetPuzzlePiecesPolicies()
    {
        var policyCollectionGrain = this.GrainFactory.GetGrain<IPolicyListGrain>(Constants.SingletonGrain);
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
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.SetNickname(command.Nickname);
        await UpdateWalletState(new UpdateUserWalletStateCommand(command.UtxoAssets, command.PaymentAddress));
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

    public override IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var subscriptionNames = @event switch
        {
            UserWalletWentOffline => this.GetPrimaryKeyString().ToSingletonList(),
            UtxoReservationStateChanged changed => new List<string>{ UtxoUtility.BuildUtxoSubscriptionName(changed.Utxo.TxId, changed.Utxo.OutputIndexOnTx), Constants.ActiveUtxoReservationsListGrainKey}, 
            _ => string.Empty.ToSingletonList(),
        };

        return subscriptionNames;
    }
    
    public async Task<GetStateResponse> GetState()
    {
        State.DomainAggregate.ThrowIfNull();
        var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
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

        var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
        var puzzlePiecesIncludingNotOwned = await activePuzzlePieceListGrain.GetActivePuzzlePieces(puzzlePieceIds);
        var response = puzzlePiecesIncludingNotOwned with { PuzzlePieces = puzzlePiecesIncludingNotOwned.PuzzlePieces.Where(x => x.IsOwned).ToList() };
        
        return response;
    }

    public async Task UserWalletStateHasChanged()
    {
        await UserSignalRChannel().SendAsync(new UserWalletStateHasChanged());
    }

    public async Task<ResultOrError<MakeAnOfferResponse>> MakeAnOffer(MakeAnOfferCommand command)
    {
        command.ThrowIfNull();
        command.Offers.ThrowIfNullOrEmpty();
        State.DomainAggregate.ThrowIfNull();

        var errors = new List<(string nickname,string error)>();
        var offersMadeSuccessfully = 0;
        var activePuzzlePieceListGrain = GrainFactory.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);

        foreach (var offer in command.Offers)
        {
            var potentialTrade = await activePuzzlePieceListGrain.GetPotentialTrade(
                State.DomainAggregate.StakingAddress,
                offer.InitiatingPuzzlePieceId,
                offer.CounterpartyPuzzlePieceId,
                offer.CounterpartyStakingAddress);
            
            if (potentialTrade == null)
            {
                errors.Add((offer.CounterpartyNickname, Invariant($"{offer.CounterpartyNickname}: Trade is no longer available.")));
                continue;
            }

            if (potentialTrade.CounterpartyPuzzlePiece.StakingAddress != offer.CounterpartyStakingAddress.ThrowIfNull())
            {
                errors.Add((offer.CounterpartyNickname, Invariant($"{offer.CounterpartyNickname}: Trade is no longer available. Counterparty piece has changed hands.")));
                continue;
            }

            var tradeId = Guid.NewGuid();
            var tradeGrain = GrainFactory.GetGrain<ITradeGrain>(tradeId);

            var initiatingWalletReservedUtxoAssets = this.State.DomainAggregate.UtxoReservations
                .Select(x => x.Value)
                .SelectMany(x => x.Utxos).ToList();
            
            var counterpartyUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(potentialTrade.CounterpartyPuzzlePiece.StakingAddress);
            var counterpartyUserWallet = await counterpartyUserWalletGrain.GetUserWallet();
            
            var counterpartyWalletReservedUtxoAssets = counterpartyUserWallet.UtxoReservations
                .Select(x => x.Value)
                .SelectMany(x => x.Utxos).ToList();
            
            var initiatingPuzzlePieceUtxo = this.State.DomainAggregate.AvailableUtxoAssets.SingleOrDefault(x =>  x.Fingerprint == potentialTrade.InitiatingPiece.PuzzlePieceId);
            var counterpartyPuzzlePieceUtxo = counterpartyUserWallet.AvailableUtxoAssets.SingleOrDefault(x =>  x.Fingerprint == potentialTrade.CounterpartyPuzzlePiece.PuzzlePieceId);

            if (initiatingPuzzlePieceUtxo == default)
            {
                return "Could not determine utxo for initiating puzzle piece".ToFailedResponse<MakeAnOfferResponse>();
            }
            
            if (counterpartyPuzzlePieceUtxo == default)
            {
                return "Could not determine utxo for counter party puzzle piece".ToFailedResponse<MakeAnOfferResponse>();
            }
            
            var tradeResponse = await tradeGrain.CreateTrade(new CreateTradeCommand(
                potentialTrade, 
                initiatingWalletReservedUtxoAssets, 
                counterpartyWalletReservedUtxoAssets,
                new Utxo(initiatingPuzzlePieceUtxo.TxId, initiatingPuzzlePieceUtxo.OutputIndexOnTx).ToSingletonList(),
                new Utxo(counterpartyPuzzlePieceUtxo.TxId, counterpartyPuzzlePieceUtxo.OutputIndexOnTx).ToSingletonList()));

            if (tradeResponse.HasErrors)
            {
                foreach(var error in tradeResponse.Errors)
                {
                    errors.Add((offer.CounterpartyNickname, error));
                }
                continue;
            }
            
            offersMadeSuccessfully++;
        }

        return new MakeAnOfferResponse(offersMadeSuccessfully, command.Offers.Count, errors).ToSuccessResponse();
    }

    public async Task<ResultOrError<GetTradeResponse>> GetActiveTradeList(GetActiveTradeListRequest request)
    {
        State.DomainAggregate.ThrowIfNull();
        var userWalletActiveTradeListGrain = GrainFactory.GetGrain<IUserWalletActiveTradeListGrain>(this.GetPrimaryKeyString());
        var activeTrades = await userWalletActiveTradeListGrain.GetActiveTrades();

        List<Domain.Entities.UserWalletActiveTradeListAggregate.Trade> offersMade = new ();
        List<Domain.Entities.UserWalletActiveTradeListAggregate.Trade> offersReceived = new ();

        foreach (var trade in activeTrades)
        {
            if (trade.TradeDetail.InitiatingPiece.StakingAddress == State.DomainAggregate.StakingAddress)
            {
                offersMade.Add(trade);
            }
            else
            {
                offersReceived.Add(trade);
            }
        }
        
        var response = new GetTradeResponse(
            offersMade.AsEnumerable(),
            offersReceived.AsEnumerable());
        
        return response.ToSuccessResponse();
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