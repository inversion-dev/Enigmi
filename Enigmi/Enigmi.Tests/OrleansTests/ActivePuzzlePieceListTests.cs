using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Messages.SignalRMessage;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;
using Xunit.Abstractions;

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
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1));
            }
            
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssets));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(0);
            var walletState = await activePuzzlePieceListGrain.GetActivePuzzlePieces(stakeAddress);
            walletState.PuzzlePieces.Count.Should().Be(4);
            walletState.PuzzlePieces.Where(x => x.IsOwned).Sum(x => x.OwnedPuzzlePieceIds.Count).Should().Be(2);
        }
        
        [Fact]
        public async Task ShouldSucceedWhenRequestingWalletState2()
        {
            var stakeAddress1 = "stake_address_xxxx4";
            var (userWalletGrain1, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient,stakeAddress1, quantityToBuy: 2);
            
            var stakeAddress2 = "stake_address_xxxx5";
            var (userWalletGrain2, orderGrain2) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient,stakeAddress2, quantityToBuy: 2);
            
            //UI will trigger signalR message to update wallet state
            var order1 = await orderGrain1.GetOrder();
            var utxoAssetsGroup1 = new List<UtxoAsset>();
            foreach (var orderedPuzzlePiece in order1.OrderedPuzzlePieces)
            {
                utxoAssetsGroup1.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1));
            }
            await userWalletGrain1.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssetsGroup1));
            
            var order2 = await orderGrain2.GetOrder();
            var utxoAssetsGroup2 = new List<UtxoAsset>();
            foreach (var orderedPuzzlePiece in order2.OrderedPuzzlePieces)
            {
                utxoAssetsGroup2.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1));
            }
            await userWalletGrain2.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssetsGroup2));

            var puzzlePieceId = order1.OrderedPuzzlePieces.First().Id;
            var puzzlePieceGrain = ClusterClient.GetGrain<IPuzzlePieceGrain>(puzzlePieceId);
            
            var puzzlePiece = await puzzlePieceGrain.GetPuzzlePiece();
            puzzlePiece.ThrowIfNull();

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(0);
            var tradeResponse = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, puzzlePiece.PuzzlePieceDefinitionId);

            // var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(0);
            // var walletState = await activePuzzlePieceListGrain.GetActivePuzzlePieces(stakeAddress1);
            // walletState.PuzzlePieces.Count.Should().Be(4);
            // walletState.PuzzlePieces.Where(x => x.IsOwned).Sum(x => x.OwnedPuzzlePieceIds.Count).Should().Be(2);
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
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1));
            }
            
            var (_, orderGrain2) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient,stakeAddress, quantityToBuy: 2);
            var order2 = await orderGrain2.GetOrder();
            
            foreach (var orderedPuzzlePiece in order2.OrderedPuzzlePieces)
            {
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1));
            }
            
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssets));

            var orderState = await userWalletGrain.GetActiveCompletedOrderPuzzlePieces(order2.Id);
            orderState.ThrowIfNull();
            orderState.PuzzlePieces.Sum(x => x.OwnedPuzzlePieceIds.Count).Should().Be(2);

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(0);
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
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1));
            }
            
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssets));

            var walletState = await userWalletGrain.GetState();
            walletState.PuzzlePieces.Count.Should().Be(4);
            walletState.PuzzlePieces.Count(x => x.IsOwned).Should().Be(1);
            
            var settingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(0);
            var settings = await settingsGrain.GetSettings();
            settings.UserWalletRoundTripPingInterval = TimeSpan.FromSeconds(2);
            settings.UserWalletOnlineIdleTimeout = TimeSpan.FromSeconds(4);
            await settingsGrain.UpdateSettings(settings);

            await userWalletGrain.Connect(new ConnectUserCommand(utxoAssets));

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