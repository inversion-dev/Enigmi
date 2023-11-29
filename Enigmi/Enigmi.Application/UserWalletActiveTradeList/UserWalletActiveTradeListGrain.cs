using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Domain.Entities.TradeAggregate.Events;
using Enigmi.Domain.ValueObjects;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWalletActiveTradeList;
using Enigmi.Messages.SignalRMessage;
using Orleans.Providers;

namespace Enigmi.Application.UserWalletActiveTradeList;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class UserWalletActiveTradeListGrain : GrainBase<Domain.Entities.UserWalletActiveTradeListAggregate.UserWalletActiveTradeList>, IUserWalletActiveTradeListGrain
{
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.DomainAggregate == null)
        {
            State.DomainAggregate = new Domain.Entities.UserWalletActiveTradeListAggregate.UserWalletActiveTradeList(this.GetPrimaryKeyString());
            await WriteStateAsync();
        }
        
        await Subscribe<TradeCreated>(this.GetPrimaryKeyString(), OnTradeCreated);
        await Subscribe<WentOffline>(this.GetPrimaryKeyString(), OnWentOffline);
        await Subscribe<TradeSignedByCounterparty>(this.GetPrimaryKeyString(), OnTradeSignedByCounterparty);
        await Subscribe<TradeSignedByInitiatedParty>(this.GetPrimaryKeyString(), OnTradeSignedByInitiatedParty);
        await Subscribe<TradeCompleted>(this.GetPrimaryKeyString(), OnTradeCompleted);
        await Subscribe<TradeBlockchainStatusChanged>(this.GetPrimaryKeyString(), OnTradeBlockchainStatusChanged);
        await Subscribe<TradeBlockchainSubmissionFailed>(this.GetPrimaryKeyString(), OnTradeBlockchainSubmissionFailed);
        await Subscribe<TradeCancelled>(this.GetPrimaryKeyString(), OnTradeCancelled);

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task OnTradeCancelled(TradeCancelled @event)
    {
        @event.ThrowIfNull();
        await UpdateCachedGrain(@event.TradeId);
    }

    private async Task OnTradeBlockchainSubmissionFailed(TradeBlockchainSubmissionFailed @event)
    {
        @event.ThrowIfNull();
        await UpdateCachedGrain(@event.TradeId);
    }


    private async Task OnWentOffline(WentOffline @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.RemoveTrade(@event.TradeId);
        await WriteStateAsync();
    }

    private async Task OnTradeCreated(TradeCreated @event)
    {
        @event.ThrowIfNull();
        State.DomainAggregate.ThrowIfNull();

        var tradeGrain = GrainFactory.GetGrain<ITradeGrain>(@event.TradeId);
        var trade = await tradeGrain.GetTrade();
        trade.ThrowIfNull();
        
        State.DomainAggregate.AddTrade(new Domain.Entities.UserWalletActiveTradeListAggregate.Trade(
            @event.TradeId,
            trade.TradeDetail, 
            trade.State,
            trade.InitiatingPartySignUtcDeadline,
            Convert.ToInt32(trade.InitiatingPartySignatureDeadlineTimespan.TotalSeconds)
        ));
        await WriteStateAsync();


        if (this.GetPrimaryKeyString() == trade.TradeDetail.CounterpartyPuzzlePiece.StakingAddress)
        {
            var counterUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(trade.TradeDetail.CounterpartyPuzzlePiece.StakingAddress);
            await counterUserWalletGrain.SendSignalRMessage(new OfferReceived());    
        }

        if (this.GetPrimaryKeyString() == trade.TradeDetail.InitiatingPiece.StakingAddress)
        {
            var initiatingUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(trade.TradeDetail.InitiatingPiece.StakingAddress);
            await initiatingUserWalletGrain.SendSignalRMessage(new OfferMade());
        }
    }
    
    private async Task OnTradeSignedByCounterparty(TradeSignedByCounterparty @event)
    {
        @event.ThrowIfNull();
        await UpdateCachedGrain(@event.TradeId);
    }
    
    private async Task OnTradeSignedByInitiatedParty(TradeSignedByInitiatedParty @event)
    {
        @event.ThrowIfNull();
        await UpdateCachedGrain(@event.TradeId);
    }
    
    private async Task OnTradeCompleted(TradeCompleted @event)
    {
        @event.ThrowIfNull();
        await UpdateCachedGrain(@event.TradeId);
    }
    
    private async Task OnTradeBlockchainStatusChanged(TradeBlockchainStatusChanged @event)
    {
        @event.ThrowIfNull();
        await UpdateCachedGrain(@event.TradeId);
    }

    private async Task UpdateCachedGrain(Guid tradeId)
    {
        State.DomainAggregate.ThrowIfNull();
        var tradeGrain = GrainFactory.GetGrain<ITradeGrain>(tradeId);
        var trade = await tradeGrain.GetTrade();
        trade.ThrowIfNull();

        var activeTrade = State.DomainAggregate.ActiveTrades.SingleOrDefault(x => x.Id == tradeId);
        if (activeTrade != null)
        {
            State.DomainAggregate.UpdateTrade(activeTrade with
            {
                TradeState = trade.State, 
                InitiatingPartySignUtcDeadline = trade.InitiatingPartySignUtcDeadline
            });

            await WriteStateAsync();
            
            if (trade.TradeDetail.CounterpartyPuzzlePiece.StakingAddress == this.GetPrimaryKeyString())
            {
                var counterUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(trade.TradeDetail.CounterpartyPuzzlePiece.StakingAddress);
                await counterUserWalletGrain.SendSignalRMessage(new TradeUpdated(tradeId));
            }

            if (trade.TradeDetail.InitiatingPiece.StakingAddress == this.GetPrimaryKeyString())
            {
                var initiatingUserWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(trade.TradeDetail.InitiatingPiece.StakingAddress);
                await initiatingUserWalletGrain.SendSignalRMessage(new TradeUpdated(tradeId));
            }
        }
    }

    public override IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event)
    {
        @event.ThrowIfNull();
        return string.Empty.ToSingletonList();
    }

    public Task<IEnumerable<Domain.Entities.UserWalletActiveTradeListAggregate.Trade>> GetActiveTrades()
    {
        State.DomainAggregate.ThrowIfNull();
        return Task.FromResult(State.DomainAggregate.ActiveTrades.Where(x => x.TradeState is not TradeState.Cancelled
                                                                            and not TradeState.SubmissionFailed
                                                                            and not TradeState.Completed)
            .ToList()
            .AsEnumerable());
    }

    public Task Create()
    {
        return Task.CompletedTask;
    }
}