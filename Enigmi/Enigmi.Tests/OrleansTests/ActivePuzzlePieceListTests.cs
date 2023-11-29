using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardanoSharp.Wallet.CIPs.CIP14.Extensions;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Domain.Entities.PuzzlePieceAggregate;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;
using Xunit.Abstractions;
using static System.FormattableString;

namespace Enigmi.Tests.OrleansTests;

public class ActivePuzzlePieceListTests
{
    [Collection(ClusterCollection.Name)]
    public class UpdateActivePuzzlePiecesCommandTests : OrleansTestBase
    {
        private ClusterFixture Fixture { get; set; }

        private ITestOutputHelper TestOutputHelper { get; }

        private IClusterClient ClusterClient { get; }

        public UpdateActivePuzzlePiecesCommandTests(ClusterFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture)
        {
            Fixture = fixture;
            TestOutputHelper = testOutputHelper;
            ClusterClient = fixture.ClusterClient;
        }
        
        [Fact]
        public async Task ShouldSucceedWhenRequestingWalletState()
        {
            var stakeAddress = "stake_address_xxxx4";
            var (userWalletGrain, orderGrain) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient,stakeAddress, quantityToBuy: 2);
            
            //UI will trigger signalR message to update wallet state
            var order = await orderGrain.GetOrder();
            var utxoAssets = new List<UtxoAsset>();
            foreach (var orderedPuzzlePiece in order.OrderedPuzzlePieces)
            {
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1, orderedPuzzlePiece.BlockchainAssetId.ToAssetFingerprint()));
            }
            
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssets,"aaa"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var walletState = await activePuzzlePieceListGrain.GetActivePuzzlePieces(stakeAddress);
            walletState.PuzzlePieces.Count.Should().Be(4);
            walletState.PuzzlePieces.Where(x => x.IsOwned).Sum(x => x.OwnedPuzzlePieceIds.Count).Should().Be(2);
        }
        
        [Fact]
        public async Task ShouldSucceedWhenRequestingPotentialTrades()
        {
            var sniffer = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey); 
            var seedPuzzleCollectionResponse = await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi4")));
                        
            var stakeAddress1 = "stake_address_xxxx4";
            var (userWalletGrain1, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient,stakeAddress1, quantityToBuy: 16, seedPuzzleCollectionResponse.Result!.PuzzleCollectionId);
            
            var stakeAddress2 = "stake_address_xxxx5";
            var userWalletGrain2 = ClusterClient.GetGrain<IUserWalletGrain>(stakeAddress2);
            
            //UI will trigger signalR message to update wallet state
            var order1 = await orderGrain1.GetOrder();
            var boughtPuzzlePieces = new List<PuzzlePiece>();
            var utxoAssetsGroup = new List<UtxoAsset>();
            foreach (var orderedPuzzlePiece in order1.OrderedPuzzlePieces)
            {
                var puzzlePieceGrain = ClusterClient.GetGrain<IPuzzlePieceGrain>(orderedPuzzlePiece.Id);
                var puzzlePiece = await puzzlePieceGrain.GetPuzzlePiece();
                boughtPuzzlePieces.Add(puzzlePiece.ThrowIfNull());
                utxoAssetsGroup.Add(new UtxoAsset(puzzlePiece.Id, 1, orderedPuzzlePiece.BlockchainAssetId, 1, orderedPuzzlePiece.BlockchainAssetId.ToAssetFingerprint()));
            }
            
            await TestCompletedPuzzlePieceTrade(boughtPuzzlePieces, userWalletGrain1, userWalletGrain2, stakeAddress1);
            await TestDuplicatePuzzlePieceTrade(boughtPuzzlePieces, userWalletGrain1, userWalletGrain2, stakeAddress1);
            await TestTradePuzzlePieceForAnotherPuzzlePiece(boughtPuzzlePieces, userWalletGrain1, userWalletGrain2, stakeAddress1);
            await TestGainingDuplicatePuzzlePiece(boughtPuzzlePieces, userWalletGrain1, userWalletGrain2, stakeAddress1);
            
        }
        
        private async Task TestTradePuzzlePieceForAnotherPuzzlePiece(List<PuzzlePiece> boughtPuzzlePieces, IUserWalletGrain userWalletGrain1,
            IUserWalletGrain userWalletGrain2, string stakeAddress1)
        {
            var firstPuzzlePiece = boughtPuzzlePieces.First();
            var secondPuzzlePieceOfSameDefinition = boughtPuzzlePieces.First(x => x.PuzzlePieceDefinitionId == firstPuzzlePiece.PuzzlePieceDefinitionId && x.Id != firstPuzzlePiece.Id);

            await userWalletGrain1.UpdateWalletState(new UpdateUserWalletStateCommand(secondPuzzlePieceOfSameDefinition
                .ToSingletonList()
                .Select(TestUtil.ConvertToUtxoAsset)
                .ToList(),"aaa"));
            
            await userWalletGrain2.UpdateWalletState(new UpdateUserWalletStateCommand(firstPuzzlePiece
                .ToSingletonList()
                .Select(TestUtil.ConvertToUtxoAsset)
                .ToList(), "aaa"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var tradeResponse = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, firstPuzzlePiece.PuzzlePieceDefinitionId);

            tradeResponse.UserWalletTradeDetails.Count.Should().Be(1);
            var firstTrade = tradeResponse.UserWalletTradeDetails.First().TradeDetails.First();
            firstTrade.Rating.Should().Be(5M);
            firstTrade.CounterpartyPuzzlePiece.Rating.Should().Be(5); 
        }
        
        private async Task TestGainingDuplicatePuzzlePiece(List<PuzzlePiece> boughtPuzzlePieces, IUserWalletGrain userWalletGrain1,
            IUserWalletGrain userWalletGrain2, string stakeAddress1)
        {
            var firstPuzzlePiece = boughtPuzzlePieces.First();
            var puzzlePieceOfSameDefinition = boughtPuzzlePieces.First(x =>
                x.PuzzlePieceDefinitionId == firstPuzzlePiece.PuzzlePieceDefinitionId && x.Id != firstPuzzlePiece.Id);

            var differentRandomPuzzle = boughtPuzzlePieces.First(x => x.PuzzlePieceDefinitionId != firstPuzzlePiece.PuzzlePieceDefinitionId);

            await userWalletGrain1.UpdateWalletState(new UpdateUserWalletStateCommand(new List<PuzzlePiece>{puzzlePieceOfSameDefinition, differentRandomPuzzle}
                .Select(TestUtil.ConvertToUtxoAsset)
                .ToList(), "aaa"));
            
            await userWalletGrain2.UpdateWalletState(new UpdateUserWalletStateCommand(firstPuzzlePiece
                .ToSingletonList()
                .Select(TestUtil.ConvertToUtxoAsset)
                .ToList(),"aaa"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var tradeResponse = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, firstPuzzlePiece.PuzzlePieceDefinitionId);

            tradeResponse.UserWalletTradeDetails.First().TradeDetails.Count.Should().Be(2);
            var duplicateTrade = tradeResponse.UserWalletTradeDetails.First().TradeDetails.First(x => x.InitiatingPiece.PuzzlePieceDefinitionId == differentRandomPuzzle.PuzzlePieceDefinitionId);
            duplicateTrade.Rating.Should().Be(2.5M);
            duplicateTrade.InitiatingPiece.Rating.Should().Be(0); //Trade will result in a puzzle gaining a new duplicate piece 
        }
        
        private async Task TestCompletedPuzzlePieceTrade(List<PuzzlePiece> boughtPuzzlePieces, IUserWalletGrain userWalletGrain1,
            IUserWalletGrain userWalletGrain2, string stakeAddress1)
        {
            var distinctPuzzlePieceDefinitionIds = boughtPuzzlePieces.Select(x => x.PuzzlePieceDefinitionId).Distinct();
            distinctPuzzlePieceDefinitionIds.Count().Should().Be(4);
            
            var groupedByPuzzleDefinitions = boughtPuzzlePieces.GroupBy(x => x.PuzzlePieceDefinitionId)
                .Select(x => new { x.Key, List = x.ToList() }).ToList();

            var completedPuzzlePiecesSet = new List<PuzzlePiece>();
            foreach (var group in groupedByPuzzleDefinitions)
            {
                completedPuzzlePiecesSet.Add(group.List.First());
            }

            var randomPiece = boughtPuzzlePieces.Except(completedPuzzlePiecesSet).First();

            var anyOtherPuzzleDefinitionUtxoAsset = 
                randomPiece
                .ToSingletonList()
                .Select(TestUtil.ConvertToUtxoAsset)
                .ToList();

            await userWalletGrain1.UpdateWalletState(new UpdateUserWalletStateCommand(anyOtherPuzzleDefinitionUtxoAsset, "aaa"));
            await userWalletGrain2.UpdateWalletState(new UpdateUserWalletStateCommand(completedPuzzlePiecesSet.Select(TestUtil.ConvertToUtxoAsset).ToList(),"aa"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var tradeResponse = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, randomPiece.PuzzlePieceDefinitionId);

            tradeResponse.UserWalletTradeDetails.Count.Should().Be(1);
            var firstTrade = tradeResponse.UserWalletTradeDetails.First().TradeDetails.First();
            firstTrade.Rating.Should().Be(2.5M);
            firstTrade.CounterpartyPuzzlePiece.Rating.Should().Be(0); //Trade will result in a complete puzzle no longer being complete
        }

        private async Task TestDuplicatePuzzlePieceTrade(List<PuzzlePiece> boughtPuzzlePieces, IUserWalletGrain userWalletGrain1,
            IUserWalletGrain userWalletGrain2, string stakeAddress1)
        {
            var boughtPuzzlePiece = boughtPuzzlePieces.First();
            var twoPuzzlePiecesOfSameDefinition = boughtPuzzlePieces
                .Where(x => x.PuzzlePieceDefinitionId == boughtPuzzlePiece.PuzzlePieceDefinitionId)
                .Take(2)
                .ToList();

            var anyOtherPuzzleDefinition = boughtPuzzlePieces
                .First(x => x.PuzzlePieceDefinitionId != boughtPuzzlePiece.PuzzlePieceDefinitionId)
                .ToSingletonList()
                .Select(TestUtil.ConvertToUtxoAsset)
                .ToList();

            await userWalletGrain1.UpdateWalletState(new UpdateUserWalletStateCommand(anyOtherPuzzleDefinition, "aaa"));
            await userWalletGrain2.UpdateWalletState(new UpdateUserWalletStateCommand(twoPuzzlePiecesOfSameDefinition.Select(TestUtil.ConvertToUtxoAsset).ToList(), "aaa"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var tradeResponse = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, boughtPuzzlePiece.PuzzlePieceDefinitionId);

            tradeResponse.UserWalletTradeDetails.Count.Should().Be(1);
            var firstTrade = tradeResponse.UserWalletTradeDetails.First().TradeDetails.First();
            firstTrade.Rating.Should().Be(7.5M);
            firstTrade.CounterpartyPuzzlePiece.Rating.Should().Be(10); //Leaving Puzzle piece was a duplicate. - 10
        }

        [Fact]
        public async Task ShouldSucceedWhenRequestingWalletStateWithMultipleOrders()
        {
            var stakeAddress = "stake_address_xxxx4";
            var (userWalletGrain, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient,stakeAddress, quantityToBuy: 2);
            
            //UI will trigger signalR message to update wallet state
            var order = await orderGrain1.GetOrder();
            var utxoAssets = new List<UtxoAsset>();
            foreach (var orderedPuzzlePiece in order.OrderedPuzzlePieces)
            {
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1, orderedPuzzlePiece.BlockchainAssetId.ToAssetFingerprint()));
            }
            
            var (_, orderGrain2) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient,stakeAddress, quantityToBuy: 2);
            var order2 = await orderGrain2.GetOrder();
            
            foreach (var orderedPuzzlePiece in order2.OrderedPuzzlePieces)
            {
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1, orderedPuzzlePiece.BlockchainAssetId));
            }
            
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssets, "aaa"));

            var orderState = await userWalletGrain.GetActiveCompletedOrderPuzzlePieces(order2.Id);
            orderState.ThrowIfNull();
            orderState.PuzzlePieces.Sum(x => x.OwnedPuzzlePieceIds.Count).Should().Be(2);

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var walletState = await activePuzzlePieceListGrain.GetActivePuzzlePieces(stakeAddress);
            walletState.PuzzlePieces.Count.Should().Be(4);
            walletState.PuzzlePieces.Where(x => x.IsOwned).Sum(x => x.OwnedPuzzlePieceIds.Count) .Should().Be(4);
        }
        
        [Fact]
        public async Task ShouldClearActivePuzzlePiecesWhenUserWalletGoesOffline()
        {
            var (userWalletGrain, orderGrain) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient);
            
            //UI will trigger signalR message to update wallet state
            var order = await orderGrain.GetOrder();
            var utxoAssets = new List<UtxoAsset>();
            foreach (var orderedPuzzlePiece in order.OrderedPuzzlePieces)
            {
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1, orderedPuzzlePiece.BlockchainAssetId.ToAssetFingerprint()));
            }
            
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssets, "aaa"));

            var walletState = await userWalletGrain.GetState();
            walletState.PuzzlePieces.Count.Should().Be(4);
            walletState.PuzzlePieces.Count(x => x.IsOwned).Should().Be(1);
            
            var settingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
            var settings = await settingsGrain.GetSettings();
            settings.UserWalletRoundTripPingInterval = TimeSpan.FromSeconds(2);
            settings.UserWalletOnlineIdleTimeout = TimeSpan.FromSeconds(4);
            await settingsGrain.UpdateSettings(settings);

            await userWalletGrain.Connect(new ConnectUserCommand(utxoAssets, "abc", "aaa"));

            await Task.Delay(TimeSpan.FromSeconds(3));
            await userWalletGrain.ReplyToClientPing(); //simulate ping
            var userWallet = await userWalletGrain.GetUserWallet();
            userWallet.OnlineState.Should().Be(OnlineState.Online);

            await Task.Delay(TimeSpan.FromSeconds(6));
            var userWallet2 = await userWalletGrain.GetUserWallet();
            userWallet2.OnlineState.Should().Be(OnlineState.Offline);
            
            await TestUtil.Retry(2, 10, async () =>
            {
                var walletStateAfterGoingOffLine = await userWalletGrain.GetState();
                return !walletStateAfterGoingOffLine.PuzzlePieces.Any();
            });
            
            var walletStateAfterGoingOffLine = await userWalletGrain.GetState();
            walletStateAfterGoingOffLine.PuzzlePieces.Count.Should().Be(0);
            walletStateAfterGoingOffLine.PuzzlePieces.Count(x => x.IsOwned).Should().Be(0);
        }
    }
}