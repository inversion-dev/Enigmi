using CardanoSharp.Wallet.Models.Addresses;
using Domain.ValueObjects;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;
using Enigmi.Domain.Entities.TradeAggregate.Events;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate.Events;
using Enigmi.Domain.Utils;
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
public class TradeGrain : GrainBase<Domain.Entities.TradeAggregate.Trade>,IRemindable, ITradeGrain
{
	private IBlockchainService BlockchainService { get; }
	
	private const string ReleaseReservedUtxosReminder = "ReleaseReservedUtxosReminder";
	public TradeGrain(IBlockchainService blockchainService)
	{
		BlockchainService = blockchainService.ThrowIfNull();
	}

	private async Task CancelReleaseReservedUtxosReminder()
	{
		var reminderReference = await this.GetReminder(ReleaseReservedUtxosReminder);
		await this.UnregisterReminder(reminderReference);
	}

	private async Task SetReleaseReservedUtxosReminder(TimeSpan timespan)
	{
		await this.RegisterOrUpdateReminder(ReleaseReservedUtxosReminder, timespan, timespan);
	}
	
	public async Task ReceiveReminder(string reminderName, TickStatus status)
	{
		if (State.DomainAggregate == null)
		{
			await CancelReleaseReservedUtxosReminder();
			return;
		}
		
		State.DomainAggregate.ThrowIfNull();

		if (State.DomainAggregate.State == TradeState.CounterpartySigned && State.DomainAggregate.InitiatingPartySignUtcDeadline < DateTime.UtcNow)
		{
			var initiatingUserWallet = await InitiatingUserWalletGrain.GetUserWallet();
			var reserveBy = initiatingUserWallet.GetReserveByKey(Reserver.Trade, State.DomainAggregate.Id);
			await InitiatingUserWalletGrain.ReleaseUtxoReservations(reserveBy);
			await CounterpartyUserWalletGrain.ReleaseUtxoReservations(reserveBy);
			
			await CancelReleaseReservedUtxosReminder();
		}

		if (State.DomainAggregate.State != TradeState.CounterpartySigned)
		{
			await CancelReleaseReservedUtxosReminder();
		}

		await WriteStateAsync();
	}
	
	public override async Task OnActivateAsync(CancellationToken cancellationToken)
	{
		var subscriptionKey = this.GetGrainId().GetGuidKey().ToString();
		await Subscribe<BlockchainTransactionFailed>(subscriptionKey, OnBlockchainTransactionFailed);
		await Subscribe<BlockchainTransactionSucceeded>(subscriptionKey, OnBlockchainTransactionSucceeded);
		await Subscribe<BlockchainTransactionStateUpdated>(subscriptionKey, OnBlockchainTransactionStateUpdated);
		await Subscribe<BlockchainTransactionSubmitted>(subscriptionKey, OnBlockchainTransactionSubmitted);
		await Subscribe<TradeSignedByCounterparty>(subscriptionKey, OnTradeSignedByCounterparty);

		if (State.DomainAggregate != null)
		{
			await SubscribeRelatedUtxos();
		}

		await base.OnActivateAsync(cancellationToken);
	}

	private async Task OnTradeSignedByCounterparty(TradeSignedByCounterparty @event)
	{
		State.DomainAggregate.ThrowIfNull();
		@event.ThrowIfNull();
		
		var initiatingUserWallet = await InitiatingUserWalletGrain.GetUserWallet();
		var reserveBy = initiatingUserWallet.GetReserveByKey(Reserver.Trade, State.DomainAggregate.Id);

		try
		{
			var initiatingWalletUtxo = State.DomainAggregate.BlockchainTransaction!.InitiatingPartyUsedUtxos!
				.Select(y => new Utxo(y.TxHash!, y.TxIndex))
				.ToList();
			
			await InitiatingUserWalletGrain.ReserveUtxos(
				initiatingWalletUtxo,
				new List<string>{State.DomainAggregate.TradeDetail.InitiatingPiece.PuzzlePieceId, },
				reserveBy);

			var counterpartyWalletUtxos = State.DomainAggregate.BlockchainTransaction!.CounterPartyUsedUtxos!
				.Select(y => new Utxo(y.TxHash!, y.TxIndex))
				.ToList();
			
			await CounterpartyUserWalletGrain.ReserveUtxos(
				counterpartyWalletUtxos,
				new List<string>{State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.PuzzlePieceId },
				reserveBy);

			await SetReleaseReservedUtxosReminder(State.DomainAggregate.InitiatingPartySignatureDeadlineTimespan);
		}
		catch
		{
			await InitiatingUserWalletGrain.ReleaseUtxoReservations(reserveBy);
			await CounterpartyUserWalletGrain.ReleaseUtxoReservations(reserveBy);		
			State.DomainAggregate.CancelTrade();
		}

		await WriteStateAsync();
	}

	private async Task OnBlockchainTransactionSubmitted(BlockchainTransactionSubmitted @event)
	{
		State.DomainAggregate.ThrowIfNull();
		@event.ThrowIfNull();
        State.DomainAggregate.MarkAsSubmitted();
        await WriteStateAsync();
	}

	private async Task OnBlockchainTransactionStateUpdated(BlockchainTransactionStateUpdated @event)
	{
		State.DomainAggregate.ThrowIfNull();
		@event.ThrowIfNull();
		State.DomainAggregate.UpdateNumberOfConfirmations(@event.NumberOfConfirmations);
		await WriteStateAsync();
	}

	private async Task OnBlockchainTransactionSucceeded(BlockchainTransactionSucceeded @event)
	{
		State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.MarkAsCompleted();
        await UnsubscribeFromRelatedUtxoEvents();
        await WriteStateAsync();
        
        await InitiatingUserWalletGrain.UserWalletStateHasChanged();
        await CounterpartyUserWalletGrain.UserWalletStateHasChanged();
	}

	private async Task OnBlockchainTransactionFailed(BlockchainTransactionFailed @event)
	{
		State.DomainAggregate.ThrowIfNull();
		@event.ThrowIfNull();
		
		if (@event.IsDoubleSpent)
		{
			State.DomainAggregate.CancelTrade();
			await UnsubscribeFromRelatedUtxoEvents();
		}
		else
		{
			State.DomainAggregate.BlockchainTransactionFailed();
		}

		await WriteStateAsync();
	}

	private async Task UnsubscribeFromRelatedUtxoEvents()
	{
		State.DomainAggregate.ThrowIfNull();
		foreach (var utxo in State.DomainAggregate.PuzzlePieceRelatedUtxos)
		{
			await Unsubscribe(UtxoUtility.BuildUtxoSubscriptionName(utxo.TxId, utxo.OutputIndexOnTx));
		}
	}

	public async Task<ResultOrError<CreateTradeResponse>> CreateTrade(CreateTradeCommand command)
    {
        command.ThrowIfNull();

        if (State.DomainAggregate != null)
        {
            throw new ApplicationException("Trade already created");
        }

        command.InitiatingPuzzlePieceUtxos.ThrowIfNullOrEmpty();
        command.CounterpartyPuzzlePieceUtxos.ThrowIfNullOrEmpty();
        
        var grainSettingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
        var settings = await grainSettingsGrain.GetSettings();
        State.DomainAggregate = new Domain.Entities.TradeAggregate.Trade(this.GetGrainId().GetGuidKey(), command.TradeDetail, settings.TradeGrain.InitiatingPartySignTimespanDeadline);
        
        var relatedUtxos = command.InitiatingPuzzlePieceUtxos.Union(command.CounterpartyPuzzlePieceUtxos).ToList();
        State.DomainAggregate.SetPuzzlePieceUtxos(relatedUtxos);
        
        await SubscribeRelatedUtxos();

        var allReservedUtxos = command.CounterpartyWalletReservedUtxoAssets.Union(command.InitiatingWalletReservedUtxoAssets).ToList();
        var externalReservedUtxos = allReservedUtxos.Where(x => relatedUtxos.Any(y => x.TxId == y.TxId && x.OutputIndexOnTx == y.OutputIndexOnTx)).ToList();
        State.DomainAggregate.AddExternalReservedUtxo(externalReservedUtxos);
        
        await WriteStateAsync();

        return new CreateTradeResponse().ToSuccessResponse();
    }

	private async Task SubscribeRelatedUtxos()
	{
		State.DomainAggregate.ThrowIfNull();
		
		foreach (var utxo in State.DomainAggregate.PuzzlePieceRelatedUtxos)
		{
			await Subscribe<UtxoReservationStateChanged>(UtxoUtility.BuildUtxoSubscriptionName(utxo.TxId, utxo.OutputIndexOnTx), OnUtxoReservedEvent);
		}
	}

	private async Task OnUtxoReservedEvent(UtxoReservationStateChanged @event)
	{
		@event.ThrowIfNull();
		State.DomainAggregate.ThrowIfNull();
		
		var firstDashIndex = @event.ReserveBy.IndexOf('-');
		var idString = @event.ReserveBy.Substring(firstDashIndex + 1);
		var id = Guid.Parse(idString);
		
		if (@event.ReservationState == ReservationState.Released
		    && id != State.DomainAggregate.Id)
		{
			State.DomainAggregate.RemoveExternalReservedUtxo(@event.Utxo);
		}
		
		if (@event.ReservationState == ReservationState.Reserved 
		    && id != State.DomainAggregate.Id)
		{
			State.DomainAggregate.AddExternalReservedUtxo(@event.Utxo.ToSingletonList());
		}
		
		await InitiatingUserWalletGrain.SendSignalRMessage(new TradeAvailabilityChanged(State.DomainAggregate.Id, State.DomainAggregate.IsAvailable));
		await CounterpartyUserWalletGrain.SendSignalRMessage(new TradeAvailabilityChanged(State.DomainAggregate.Id, State.DomainAggregate.IsAvailable));
		
		await WriteStateAsync();
	}

	private IUserWalletGrain InitiatingUserWalletGrain =>
		GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate!.TradeDetail.InitiatingPiece.StakingAddress);
	
	private IUserWalletGrain CounterpartyUserWalletGrain =>
		GrainFactory.GetGrain<IUserWalletGrain>(State.DomainAggregate!.TradeDetail.CounterpartyPuzzlePiece.StakingAddress);
	
	public async Task<ResultOrError<CreateTransactionResponse>> BuildTransaction(CreateTransactionCommand command)
    {
	    command.ThrowIfNull();
	    State.DomainAggregate.ThrowIfNull();
	    var initiatingUserWalletGrain = InitiatingUserWalletGrain;
	    var counterPartyUserWallet = CounterpartyUserWalletGrain;
	    var grainSettingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
	    var settings = await grainSettingsGrain.GetSettings();

	    var initiatingUserWallet = await initiatingUserWalletGrain.GetUserWallet();
	    var counterpartyUserWallet = await counterPartyUserWallet.GetUserWallet();
	    
	    var slotAndFees = await BlockchainService.GetSlotAndFeesAsync();

	    var initiatingAsset = await InitiatingAsset(initiatingUserWallet, State.DomainAggregate.TradeDetail.InitiatingPiece.PuzzlePieceId);
	    var counterpartyAsset = await InitiatingAsset(counterpartyUserWallet, State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.PuzzlePieceId);
	    
	    if (initiatingAsset == default)
	    {
		    return "Asset not found in initiating user's wallet".ToFailedResponse<CreateTransactionResponse>();
	    }
	    
	    if (counterpartyAsset == default)
	    {
		    return "Asset not found in counter party's wallet".ToFailedResponse<CreateTransactionResponse>();
	    }

	    var createTradeTransactionResponse =  State.DomainAggregate.CreateTradeTransaction(
		    initiatingUserWallet.UnreservedUtxoAssets,
		    counterpartyUserWallet.UnreservedUtxoAssets,
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
	    
	    var wallet = await InitiatingUserWalletGrain.GetUserWallet();
	    var reserveBy = wallet.GetReserveByKey(Reserver.Trade, State.DomainAggregate.Id);

	    var initiatingUserWalletReservationResponse = await InitiatingUserWalletGrain.DoesUtxoReservationsExist(reserveBy);
	    if (initiatingUserWalletReservationResponse.HasErrors)
	    {
		    return initiatingUserWalletReservationResponse.Errors.ToFailedResponse<Constants.Unit>();
	    }

	    if (!initiatingUserWalletReservationResponse.Result)
	    {
		    return "Utxo reservation not found, possible reason is because deadline has expired".ToFailedResponse<Constants.Unit>();
	    }
	    
	    var counterpartyUserWalletReservationResponse = await CounterpartyUserWalletGrain.DoesUtxoReservationsExist(reserveBy);
	    if (counterpartyUserWalletReservationResponse.HasErrors)
	    {
		    return counterpartyUserWalletReservationResponse.Errors.ToFailedResponse<Constants.Unit>();
	    }

	    if (!counterpartyUserWalletReservationResponse.Result)
	    {
		    return "Utxo reservation not found, possible reason is because deadline has expired".ToFailedResponse<Constants.Unit>();
	    }
	    
	    var signByInitiatingParty = State.DomainAggregate.SignByInitiatingParty(command.WitnessCborHex);
	    if (signByInitiatingParty.HasErrors)
	    {
		    return signByInitiatingParty.Errors.ToFailedResponse<Constants.Unit>();
	    }
	    
	    await WriteStateAsync();
	    await CancelReleaseReservedUtxosReminder();

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
	    
	    var initiatingAsset = userWallet.AvailableUtxoAssets.SingleOrDefault(x => x.BlockchainAssetId == initiatingPuzzlePiece.BlockchainAssetId);
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
        State.DomainAggregate.WentOffline();
        await UnsubscribeFromRelatedUtxoEvents();
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
				State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress,
				State.DomainAggregate.Id.ToString()
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
            TradeBlockchainSubmissionFailed => new List<string>
            {
	            State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
	            State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
            },
            TradeCancelled => new List<string>
            {
	            State.DomainAggregate.TradeDetail.CounterpartyPuzzlePiece.StakingAddress,
	            State.DomainAggregate.TradeDetail.InitiatingPiece.StakingAddress
            },
            _ => string.Empty.ToSingletonList(),
        };

        return subscriptionNames;
    }
}