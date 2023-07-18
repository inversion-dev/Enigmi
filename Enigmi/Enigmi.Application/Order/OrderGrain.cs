using Enigmi.Application.ExtensionMethods;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;
using Enigmi.Domain.Entities.OrderAggregate.Events;
using Enigmi.Domain.Entities.OrderAggregate.ValueObjects;
using Enigmi.Domain.Entities.PuzzlePieceAggregate;
using Enigmi.Grains.Shared.BlockchainTransactionSubmission;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.Order;
using Enigmi.Grains.Shared.Order.Messages;
using Enigmi.Grains.Shared.Policy;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleDefinition;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.SystemWallet;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Infrastructure.Services.BlobStorage;
using Enigmi.Infrastructure.Services.BlockchainService;
using Enigmi.Messages.SignalRMessage;
using Microsoft.Extensions.Logging;
using Orleans.Providers;
using Orleans.Runtime;
using PeterO.Cbor2;
using ApproveOrderResponse = Enigmi.Grains.Shared.UserWallet.Messages.ApproveOrderResponse;

namespace Enigmi.Application.Order;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class OrderGrain : GrainBase<Domain.Entities.OrderAggregate.Order>, IOrderGrain
{
    private Settings Settings { get; }

    private IBlobStorageService BlobStorageService { get; }

    public OrderGrain(ILogger<OrderGrain> logger,
        IBlockchainService blockchainService,
        Settings settings,
        IBlobStorageService blobStorageService)
    {
        Settings = settings.ThrowIfNull();
        BlobStorageService = blobStorageService.ThrowIfNull();
        Logger = logger.ThrowIfNull();
        BlockchainService = blockchainService.ThrowIfNull();
    }

    private ILogger<OrderGrain> Logger { get; }

    private IBlockchainService BlockchainService { get; }

    private IDisposable? CancelOrderTimer { get; set; }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        CancelOrderTimer = RegisterTimer(CancelOrderTimerHandler, this, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        var orderIdSubscription = this.GetGrainId().GetGuidKey().ToString();

        await Subscribe<BlockchainTransactionSucceeded>(orderIdSubscription, OnBlockchainTransactionOnChainConfirmed);
        await Subscribe<BlockchainTransactionFailed>(orderIdSubscription, OnBlockchainTransactionRejected);
        await Subscribe<BlockchainTransactionStateUpdated>(orderIdSubscription, OnBlockchainTransactionStateUpdate);
        await Subscribe<BlockchainTransactionSubmitted>(orderIdSubscription, OnBlockchainTransactionSubmitted);
    }

    private async Task CancelOrderTimerHandler(object state)
    {
        await this.SelfInvokeAfter<IOrderGrain>(o => o.ProcessCancelOrder());
    }

    public async Task ProcessCancelOrder()
    {
        if (State.DomainAggregate == null)
        {
            return;
        }

        var order = State.DomainAggregate;
        if (order.HasBeenSubmitted)
        {
            CancelOrderTimer?.Dispose();
        }
        else if (order.HasOrderExpired())
        {
            order.CancelOrder();
            CancelOrderTimer?.Dispose();
            await WriteStateAsync();
        }
    }

    public async Task<ResultOrError<BuildOrderResponse>> BuildOrder(BuildOrderCommand buildOrderCommand)
    {
        if (State.DomainAggregate != null)
        {
            throw new Exception("Order has already been built");
        }

        var settingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(0);
        var settings = await settingsGrain.GetSettings();

        var systemAddress = await this.GrainFactory.GetGrain<ISystemWalletGrain>(0).GetHumanFriendlyAddress();
        State.DomainAggregate = new Domain.Entities.OrderAggregate.Order(this.GetGrainId().GetGuidKey(), settings.OrderGrain.OrderExpiresTimespan);
        var order = State.DomainAggregate;

        order.SetOrderMetadata(buildOrderCommand.PuzzleCollectionId, buildOrderCommand.PuzzleSize);

        var puzzleCollectionGrain = GrainFactory.GetGrain<IPuzzleCollectionGrain>(buildOrderCommand.PuzzleCollectionId);
        var puzzleCollectionDetail = await puzzleCollectionGrain.GetPuzzleCollection();
        puzzleCollectionDetail.ThrowIfNull();
        var policyId = puzzleCollectionDetail.PuzzlePiecePolicy.PolicyId;
        if (string.IsNullOrEmpty(policyId))
        {
            throw new Exception("Expected policyId to be set on puzzle collection");
        }

        var puzzlePieceMetadata = new PuzzlePieceMetadataList();
        foreach (var id in buildOrderCommand.OrderedPuzzlePieceIds)
        {
            var puzzlePieceGrain = GrainFactory.GetGrain<IPuzzlePieceGrain>(id);
            var puzzlePiece = await puzzlePieceGrain.GetPuzzlePiece();
            puzzlePiece.ThrowIfNull();
            
            order.AddOrderedPuzzlePieceId(id, puzzlePiece.BlockchainAssetId, policyId, puzzleCollectionDetail.PuzzlePiecePriceInLovelace);

            puzzlePiece.ThrowIfNull();
            var puzzleDefinitionGrain = GrainFactory.GetGrain<IPuzzleDefinitionGrain>(puzzlePiece.PuzzleDefinitionId);
            var puzzleDefinitionDetail = await puzzleDefinitionGrain.GetPuzzleDefinition();
            puzzleDefinitionDetail.ThrowIfNull();

            await CreatePuzzlePiecePlaceholderImage(puzzlePiece);

            var cborMetadata = CBORObject.FromObject(new
            {
                metadata = CBORObject.FromJSONString(puzzleDefinitionDetail.BlockchainMetadataJson),
                name = Guid.NewGuid().ToString("N"),
                //image = Invariant($"{Settings.BlobstorageConfig.BlobStorageHost}{BlobPathHelper.PrependBlobPathIfRequired(Settings, puzzlePieceDetail.BlobImagePath)}") // "https://tinyurl.com/45nu8533"
                image = "https://tinyurl.com/yds3z7yb"
            });

            puzzlePieceMetadata.AddPuzzlePieceMetadata(id, cborMetadata.EncodeToBytes());
        }

        order.SetOrderer(buildOrderCommand.UserWalletId);

        var policyGrain = this.GrainFactory.GetGrain<IPolicyGrain>(policyId);
        var mnemonicResponse = await policyGrain.GetMnemonic();
        var policyDetails = await policyGrain.GetPolicy();
        if (mnemonicResponse.HasErrors)
        {
            return mnemonicResponse.Errors.ToFailedResponse<BuildOrderResponse>();
        }
        var slotAndFees = await BlockchainService.GetSlotAndFeesAsync();

        var createTransactionResponse = order.CreateTransaction(
            buildOrderCommand.UserWalletAssets,
            slotAndFees.Slot.Slot,
            slotAndFees.Slot.SlotUtcTimestamp,
            new Domain.ValueObjects.CardanoNetworkParameters(slotAndFees.CardanoNetworkFee.MinFeeA, slotAndFees.CardanoNetworkFee.MinFeeB),
            systemAddress,
            buildOrderCommand.PaymentAddress,
            settings.OrderBlockchainTransactionSettings.Ttl,
            puzzlePieceMetadata.List,
            mnemonicResponse.Result!.ThrowIfNullOrWhitespace(),
            policyDetails.PolicyClosingSlot);

        if (createTransactionResponse.HasErrors)
        {
            return createTransactionResponse.Errors.ToFailedResponse<BuildOrderResponse>();
        }

        await WriteStateAsync();

        var usedUtxos = order.BlockchainTransaction!.UserWalletInputBlockchainAssets
            .Select(o => o.GetUtxo())
            .Distinct()
            .ToList();

        var result = createTransactionResponse.Result.ThrowIfNull();

        return new BuildOrderResponse(
            usedUtxos,
            result.UnsignedTransactionCborHex,
            result.Fee
            ).ToSuccessResponse();
    }

    private async Task CreatePuzzlePiecePlaceholderImage(PuzzlePiece puzzlePieceDetail)
    {
        await BlobStorageService.CopyBlobAsync(@"/assets/ComingSoon.jpg", puzzlePieceDetail.BlobImagePath, true);
    }

    public async Task<ResultOrError<ApproveOrderResponse>> ApproveOrder(ApproveOrderCommand approveOrderCommand)
    {
        State.DomainAggregate.ThrowIfNull();
        var order = State.DomainAggregate;
        var approveOrderResponse = order.ApproveOrder(approveOrderCommand.WitnessCbor);
        if (approveOrderResponse.HasErrors)
        {
            return approveOrderResponse.Errors.ToFailedResponse<ApproveOrderResponse>();
        }
        await WriteStateAsync();

        var blockchainTransactionSubmissionGrain = GrainFactory.GetGrain<IBlockchainTransactionSubmissionGrain>(order.Id);
        await blockchainTransactionSubmissionGrain.Submit(order.Id, order.BlockchainTransaction!.SignedTransactionCborHex!, order.BlockchainTransaction.TtlUtcTimestamp!.Value);

        return new ApproveOrderResponse().ToSuccessResponse();
    }

    public ValueTask<Domain.Entities.OrderAggregate.Order> GetOrder()
    {
        State.DomainAggregate.ThrowIfNull();
        return ValueTask.FromResult(State.DomainAggregate);
    }

    public async Task<ResultOrError<CancelOrderResponse>> CancelOrder(CancelOrderCommand command)
    {
        command.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.CancelOrder();
        await WriteStateAsync();

        return new CancelOrderResponse().ToSuccessResponse();
    }

    public override string ResolveSubscriptionName(DomainEvent @event)
    {
        if (State.DomainAggregate == null)
        {
            return String.Empty;
        }

        var subscriptionName = @event switch
        {
            OrderCancelled => State.DomainAggregate.UserWalletId,
            _ => string.Empty
        };

        return subscriptionName ?? String.Empty;
    }

    private async Task OnBlockchainTransactionRejected(BlockchainTransactionFailed @event)
    {
        Logger.LogInformation("Order {id} received {event}", this.GetGrainId().GetGuidKey(), @event);
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.CancelOrder();
        await WriteStateAsync();

        var userWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate.UserWalletId);
        await userWalletGrain.SendSignalRMessage(new OrderFailed(State.DomainAggregate.Id));
    }

    private async Task OnBlockchainTransactionSubmitted(BlockchainTransactionSubmitted @event)
    {
        Logger.LogInformation("Order {id} received {event}", this.GetGrainId().GetGuidKey(), @event);
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.MarkAsSubmitted();
        await WriteStateAsync();
    }

    private async Task OnBlockchainTransactionOnChainConfirmed(BlockchainTransactionSucceeded @event)
    {
        Logger.LogInformation("Order {id} received {event}", this.GetGrainId().GetGuidKey(), @event);
        State.DomainAggregate.ThrowIfNull();
        await ReplacePlaceHolderImageWithActualImage();
        State.DomainAggregate.CompleteOrder();
        await WriteStateAsync();

        var userWalletGrain = this.GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate.UserWalletId);
        await userWalletGrain.SendSignalRMessage(new OrderCompleted(State.DomainAggregate.Id));
        await userWalletGrain.UserWalletStateHasChanged();
    }

    private async Task ReplacePlaceHolderImageWithActualImage()
    {
        State.DomainAggregate.ThrowIfNull();
        foreach (var orderedPuzzlePiece in State.DomainAggregate.OrderedPuzzlePieces)
        {
            var puzzlePieceId = orderedPuzzlePiece.Id;
            var puzzlePieceGrain = GrainFactory.GetGrain<IPuzzlePieceGrain>(puzzlePieceId);
            var puzzlePieceDetail = await puzzlePieceGrain.GetPuzzlePiece();
            puzzlePieceDetail.ThrowIfNull();

            var puzzleDefinitionGrain = GrainFactory.GetGrain<IPuzzleDefinitionGrain>(puzzlePieceDetail.PuzzleDefinitionId);
            var puzzleDefinitionDetail = await puzzleDefinitionGrain.GetPuzzleDefinition();
            puzzleDefinitionDetail.ThrowIfNull();

            var puzzlePieceDefinition =
                puzzleDefinitionDetail.PuzzlePieceDefinitions.SingleOrDefault(x =>
                    x.Id == puzzlePieceDetail.PuzzlePieceDefinitionId);
            puzzlePieceDefinition.ThrowIfNull();

            await BlobStorageService.CopyBlobAsync(puzzlePieceDefinition.ImageBlobPath, puzzlePieceDetail.BlobImagePath,
                true);
        }
    }

    private async Task OnBlockchainTransactionStateUpdate(BlockchainTransactionStateUpdated @event)
    {
        State.DomainAggregate.ThrowIfNull();
        Logger.LogInformation("Order {id} received {event}", this.GetGrainId().GetGuidKey(), @event);
        State.DomainAggregate.UpdateNumberOfConfirmations(@event.NumberOfConfirmations);
        await WriteStateAsync();

        var userWalletGrain = this.GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate.UserWalletId);
        await userWalletGrain.SendSignalRMessage(new OrderUpdated(@event.OrderId, @event.NumberOfConfirmations));
    }
}