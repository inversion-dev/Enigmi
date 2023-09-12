using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Tests.Utils;
using Orleans;
using Xunit;
using Enigmi.Domain.Entities.PuzzlePieceAggregate;
using Enigmi.Domain.Entities.TradeAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Domain.ValueObjects;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Grains.Shared.UserWalletActiveTradeList;
using Enigmi.Messages.SignalRMessage;
using FluentAssertions;

namespace Enigmi.Tests.OrleansTests;

public class UserWalletActiveTradeListGrainTests
{
    [Collection(ClusterCollection.Name)]

    [TestCaseOrderer("Enigmi.Tests.OrleansTests.PriorityOrderer", "Enigmi.Tests")]
    public class CreateTradeTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        private ClusterFixture Fixture { get; set; }

        public CreateTradeTests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
            Fixture = fixture;
        }



        [Fact, TestPriority(1)]
        public async Task ShouldSucceedWithTradeGrainsCreated()
        {
            var stakeAddress1 = "stake_address_xxxx6";
            var (userWalletGrain1, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient, stakeAddress1, quantityToBuy: 2);

            var stakeAddress2 = "stake_address_xxxx7";
            var userWalletGrain2 = ClusterClient.GetGrain<IUserWalletGrain>(stakeAddress2);

            //UI will trigger signalR message to update wallet state
            var order1 = await orderGrain1.GetOrder();
            var boughtPuzzlePieces = new List<PuzzlePiece>();
            foreach (var orderedPuzzlePiece in order1.OrderedPuzzlePieces)
            {
                var puzzlePieceGrain = ClusterClient.GetGrain<IPuzzlePieceGrain>(orderedPuzzlePiece.Id);
                var puzzlePiece = await puzzlePieceGrain.GetPuzzlePiece();
                boughtPuzzlePieces.Add(puzzlePiece.ThrowIfNull());
            }

            var firstPuzzle = boughtPuzzlePieces.First();
            var firstItem = firstPuzzle.ToSingletonList().Select(TestUtil.ConvertToUtxoAsset).ToList();
            var secondItem = boughtPuzzlePieces.Skip(1).First().ToSingletonList().Select(TestUtil.ConvertToUtxoAsset).ToList();

            await userWalletGrain1.Connect(new ConnectUserCommand(firstItem, "abc", "aaa"));
            await userWalletGrain2.Connect(new ConnectUserCommand(secondItem, "abc","bbb"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var trades = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, firstPuzzle.PuzzlePieceDefinitionId);
            var tradeDetail = trades.UserWalletTradeDetails.First().TradeDetails.First();
            
            var userWalletActiveTradeListGrain1 = ClusterClient.GetGrain<IUserWalletActiveTradeListGrain>(stakeAddress1);
            var userWalletActiveTradeListGrain2 = ClusterClient.GetGrain<IUserWalletActiveTradeListGrain>(stakeAddress2);

            var makeAnOfferResponse = await userWalletGrain1.MakeAnOffer(
                new MakeAnOfferCommand(new List<MakeAnOfferCommand.Offer>
                {
                    new MakeAnOfferCommand.Offer(tradeDetail.InitiatingPiece.PuzzlePieceId, 
                        tradeDetail.CounterpartyPuzzlePiece.PuzzlePieceId, 
                        tradeDetail.CounterpartyPuzzlePiece.StakingAddress,
                        tradeDetail.CounterpartyPuzzlePiece.Nickname)
                }));

            if (makeAnOfferResponse.HasErrors)
            {
                throw new Exception(makeAnOfferResponse.Errors.First());
            }

            await TestUtil.Retry(2, 25, async () =>
            {
                var activeTrades = await userWalletActiveTradeListGrain1.GetActiveTrades();
                await userWalletGrain1.PingUser();
                await userWalletGrain2.PingUser();
                
                return activeTrades.Any();
            });

            var activeTrades1 = await userWalletActiveTradeListGrain1.GetActiveTrades();
            activeTrades1.Count().Should().Be(1);

            await TestUtil.Retry(2, 25, async () =>
            {
                await userWalletGrain1.PingUser();
                await userWalletGrain2.PingUser();
                
                var activeTrades = await userWalletActiveTradeListGrain2.GetActiveTrades();
                return activeTrades.Any();
            });

            var activeTrades2 = await userWalletActiveTradeListGrain2.GetActiveTrades();
            activeTrades2.Count().Should().Be(1);

            var trade = activeTrades1.First();
            var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(trade.Id);
            var retrievedTrade = await tradeGrain.GetTrade();
            retrievedTrade.State.Should().Be(TradeState.New);
        }

        [Fact, TestPriority(2)]
        public async Task ShouldSucceedBySendingSignalrMessageIndicatingTradeIsNotAvailable()
        {
            var settingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
            var settings = await settingsGrain.GetSettings();
            settings.UserWalletRoundTripPingInterval = TimeSpan.FromSeconds(2);
            settings.UserWalletOnlineIdleTimeout = TimeSpan.FromSeconds(4);
            await settingsGrain.UpdateSettings(settings);

            var stakeAddress1 = "stake_address_xxxx6";
            var stakeAddress2 = "stake_address_xxxx7";

            var userWalletGrain1 = ClusterClient.GetGrain<IUserWalletGrain>(stakeAddress1);
            var userWalletGrain2 = ClusterClient.GetGrain<IUserWalletGrain>(stakeAddress2);

            await userWalletGrain1.Connect(new ConnectUserCommand(new List<UtxoAsset>(), "abc", "aaa"));
            await userWalletGrain2.Connect(new ConnectUserCommand(new List<UtxoAsset>(), "abc", "bbb"));
            await Task.Delay(TimeSpan.FromSeconds(3));
            await userWalletGrain1.ReplyToClientPing(); //simulate ping
            var userWallet1Retrieved1 = await userWalletGrain1.GetUserWallet();
            await userWalletGrain2.ReplyToClientPing();
            userWallet1Retrieved1.OnlineState.Should().Be(OnlineState.Online);

            await TestUtil.Retry(2, 25, async () =>
            {
                await userWalletGrain2.ReplyToClientPing();
                return Fixture.SentSignalrMessages.Any(x => x is TradeStakingAddressWentOffline);
            });

            Fixture.SentSignalrMessages.Any(x => x is TradeStakingAddressWentOffline).Should().BeTrue();

            var userWalletActiveTradeListGrain2 = ClusterClient.GetGrain<IUserWalletActiveTradeListGrain>(stakeAddress2);
            var activeTrades2 = await userWalletActiveTradeListGrain2.GetActiveTrades();
            activeTrades2.Count().Should().Be(0);
        }
    }
}