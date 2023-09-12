using CardanoSharp.Wallet.Models.Addresses;
using Domain.ValueObjects;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;
using Enigmi.Domain.Entities.TradeAggregate.Events;
using Enigmi.Domain.ValueObjects;
using Enigmi.Grains.Shared.BlockchainTransactionSubmission;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Grains.Shared.Trade.Messages;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Infrastructure.Services.BlockchainService;
using Enigmi.Messages.SignalRMessage;
using Orleans.Providers;
using Orleans.Runtime;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;

namespace Enigmi.Application.Trade;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class TradeGrain : GrainBase<Domain.Entities.TradeAggregate.Trade>, ITradeGrain
{
	private IBlockchainService BlockchainService { get; }

	public TradeGrain(IBlockchainService blockchainService)
	{
		BlockchainService = blockchainService.ThrowIfNull();
	}

	public override async Task OnActivateAsync(CancellationToken cancellationToken)
	{
		var subscriptionKey = this.GetGrainId().GetGuidKey().ToString();
		await Subscribe<BlockchainTransactionFailed>(subscriptionKey, OnBlockchainTransactionFailed);
		await Subscribe<BlockchainTransactionSucceeded>(subscriptionKey, OnBlockchainTransactionSucceeded);
		await Subscribe<BlockchainTransactionStateUpdated>(subscriptionKey, OnBlockchainTransactionStateUpdated);
		await Subscribe<BlockchainTransactionSubmitted>(subscriptionKey, OnBlockchainTransactionSubmitted);

		await base.OnActivateAsync(cancellationToken);
	}

	private async Task OnBlockchainTransactionSubmitted(BlockchainTransactionSubmitted @event)
	{
		State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.MarkAsSubmitted();
        await WriteStateAsync();
	}

	private async Task OnBlockchainTransactionStateUpdated(BlockchainTransactionStateUpdated @event)
	{
		State.DomainAggregate.ThrowIfNull();
		State.DomainAggregate.UpdateNumberOfConfirmations(@event.NumberOfConfirmations);
		await WriteStateAsync();
	}

	private async Task OnBlockchainTransactionSucceeded(BlockchainTransactionSucceeded @event)
	{
		State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.MarkAsCompleted();
        await WriteStateAsync();
        
        var initiatingUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress);
        await initiatingUserWalletGrain.UserWalletStateHasChanged();
        
        var counterpartyUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress);
        await counterpartyUserWalletGrain.UserWalletStateHasChanged();
	}

	private async Task OnBlockchainTransactionFailed(BlockchainTransactionFailed @event)
	{
		State.DomainAggregate.ThrowIfNull();
		
		if (@event.IsDoubleSpent)
		{
			State.DomainAggregate.CancelTrade();    
		}
		else
		{
			State.DomainAggregate.BlockchainTransactionFailed();
		}

		await WriteStateAsync();
	}

	public async Task<ResultOrError<CreateTradeResponse>> CreateTrade(CreateTradeCommand command)
    {
        command.ThrowIfNull();

        if (State.DomainAggregate != null)
        {
            throw new ApplicationException("Trade already created");
        }
        
        var grainSettingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
        var settings = await grainSettingsGrain.GetSettings();
        State.DomainAggregate = new Domain.Entities.TradeAggregate.Trade(this.GetGrainId().GetGuidKey(), command.TradeDetail, settings.TradeGrain.InitiatingPartySignTimespanDeadline);
        await WriteStateAsync();

        return new CreateTradeResponse().ToSuccessResponse();
    }

    public async Task<ResultOrError<CreateTransactionResponse>> BuildTransaction(CreateTransactionCommand command)
    {
	    command.ThrowIfNull();
	    State.DomainAggregate.ThrowIfNull();
	    var initiatingUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress);
	    var counterPartyUserWallet = GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress);
	    var grainSettingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
	    var settings = await grainSettingsGrain.GetSettings();

	    var initiatingUserWallet = await initiatingUserWalletGrain.GetUserWallet();
	    var counterpartyUserWallet = await counterPartyUserWallet.GetUserWallet();
	    
	    var slotAndFees = await BlockchainService.GetSlotAndFeesAsync();

	    var initiatingAsset = await InitiatingAsset(initiatingUserWallet, State.DomainAggregate.TradeDetail.InitiatingPiece.PuzzlePieceId);
	    var counterpartyAsset = await InitiatingAsset(counterpartyUserWallet, State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.PuzzlePieceId);

	    var createTradeTransactionResponse =  State.DomainAggregate.CreateTradeTransaction(
		    initiatingUserWallet.AvailableUtxoAssets,
		    counterpartyUserWallet.AvailableUtxoAssets,
		    slotAndFees.Slot.Slot,
		    slotAndFees.Slot.SlotUtcTimestamp,
		    new Address(initiatingUserWallet.PaymentAddress),
		    new Address(counterpartyUserWallet.PaymentAddress),
		    new CardanoNetworkParameters(slotAndFees.CardanoNetworkFee.MinFeeA, slotAndFees.CardanoNetworkFee.MinFeeB),
		    settings.OrderBlockchainTransactionSettings.Ttl,
		    initiatingAsset,
		    counterpartyAsset
	    );

	    if (createTradeTransactionResponse.HasErrors)
	    {
		    return createTradeTransactionResponse.Errors.ToFailedResponse<CreateTransactionResponse>();
	    }

	    await WriteStateAsync();
	    return new CreateTransactionResponse(State.DomainAggregate.BlockchainTransaction!.UnsignedTransactionCborHex!, State.DomainAggregate.BlockchainTransaction!.Fee!.Value).ToSuccessResponse();
    }

    public async Task<ResultOrError<Constants.Unit>> SignByCounterparty(SignTradeByCounterpartyCommand command)
    {
	    command.ThrowIfNull();
	    State.DomainAggregate.ThrowIfNull();
	    
	    var signByCounterPartyResponse = State.DomainAggregate.SignByCounterparty(command.WitnessCborHex);
	    if (signByCounterPartyResponse.HasErrors)
	    {
		    return signByCounterPartyResponse.Errors.ToFailedResponse<Constants.Unit>();
	    }
	    
	    await WriteStateAsync();
	    return new Constants.Unit().ToSuccessResponse();
    }

    public async Task<ResultOrError<Constants.Unit>> SignTradeByInitiatingParty(SignTradeByInitiatingPartyCommand command)
    {
	    command.ThrowIfNull();
	    State.DomainAggregate.ThrowIfNull();
	    State.DomainAggregate.BlockchainTransaction.ThrowIfNull();
	    
	    var signByInitiatingParty = State.DomainAggregate.SignByInitiatingParty(command.WitnessCborHex);
	    if (signByInitiatingParty.HasErrors)
	    {
		    return signByInitiatingParty.Errors.ToFailedResponse<Constants.Unit>();
	    }
	    await WriteStateAsync();

	    var blockchainTransactionSubmissionGrain = GrainFactory.GetGrain<IBlockchainTransactionSubmissionGrain>(this.GetGrainId().GetGuidKey());
	    var submitResponse = await blockchainTransactionSubmissionGrain.Submit(
		    this.GetGrainId().GetGuidKey(),
		    State.DomainAggregate.BlockchainTransaction.SignedTransactionCborHex!,
		    State.DomainAggregate.BlockchainTransaction.TtlUtcTimestamp!.Value);
	    
	    if (submitResponse.HasErrors)
	    {
		    return submitResponse.Errors.ToFailedResponse<Constants.Unit>();
	    }
	    
	    return new Constants.Unit().ToSuccessResponse();
    }

    private async Task<UtxoAsset> InitiatingAsset(Domain.Entities.UserWalletAggregate.UserWallet userWallet, string puzzlePieceId)
    {
	    var initiatingPuzzlePieceGrain = GrainFactory.GetGrain<IPuzzlePieceGrain>(puzzlePieceId);
	    var initiatingPuzzlePiece = await initiatingPuzzlePieceGrain.GetPuzzlePiece();
	    initiatingPuzzlePiece.ThrowIfNull();
	    var initiatingAsset = userWallet.AvailableUtxoAssets.Single(x => x.BlockchainAssetId == initiatingPuzzlePiece.BlockchainAssetId);
	    return initiatingAsset;
    }

    public Task<Domain.Entities.TradeAggregate.Trade> GetTrade()
    {
        State.DomainAggregate.ThrowIfNull();
        return Task.FromResult(State.DomainAggregate);
    }

    public async Task<ResultOrError<Constants.Unit>> GoOffline(string stakingAddress)
    {
        stakingAddress.ThrowIfNullOrWhitespace();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.CancelTrade();
        await WriteStateAsync();
        
        var trade = State.DomainAggregate;
        
        var stakingAddressToNotify = trade.TradeDetail.CounterpartyPuzzlePiece.StakingAddress == stakingAddress
            ? trade.TradeDetail.InitiatingPiece.StakingAddress
            : trade.TradeDetail.CounterpartyPuzzlePiece.StakingAddress;

        var userWalletGrainToNotify = GrainFactory.GetGrain<IUserWalletGrain>(stakingAddressToNotify);
        await userWalletGrainToNotify.SendSignalRMessage(new TradeStakingAddressWentOffline(stakingAddress));
        
        return new Constants.Unit().ToSuccessResponse();
    }

    public override IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event)
    {
        State.DomainAggregate.ThrowIfNull();
        @event.ThrowIfNull();

        var subscriptionNames = @event switch
        {
            TradeCreated => new List<string>
            {
                State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
                State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
            },
            WentOffline => new List<string>
            {
                State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
                State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
            },
            TradeSignedByCounterparty => new List<string>
			{
				State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
				State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
			},
            TradeSignedByInitiatedParty => new List<string>
            {
	            State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
	            State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
            },
            TradeCompleted => new List<string>
            {
	            State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
	            State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
            },
            TradeBlockchainStatusChanged => new List<string>
			{
	            State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
	            State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
			},
            _ => string.Empty.ToSingletonList(),
        };

        return subscriptionNames;
    }
}