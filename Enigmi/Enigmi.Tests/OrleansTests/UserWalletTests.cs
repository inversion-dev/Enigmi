using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Domain.Entities.UserWalletAggregate;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;

namespace Enigmi.Tests.OrleansTests;

public class UserWalletTests 
{
    [Collection(ClusterCollection.Name)]
    public class ConnectUserCommandTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        public ConnectUserCommandTests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldSetUtxosOnConnect()
        {
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_xxxx");
            await userWalletGrain.Connect(new ConnectUserCommand(
                new List<UtxoAsset>
                {
                    new("abc", 1, "2a709b1d9c6a442317ee1032838415a48a3c66d6863e3ccf12bb48a02a709b1d9c6a442317ee1032838415a48a3c66d6863e3ccf12bb48a0", 1),
                }));

            var userWallet = await userWalletGrain.GetUserWallet();
            userWallet.AvailableUtxoAssets.Should().HaveCount(1);
        }
        
        [Fact]
        public async Task ShouldBeOnlineWhenUserConnects()
        {
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_xxxx3");
            var userWallet = await userWalletGrain.GetUserWallet();
            userWallet.OnlineState.Should().Be(OnlineState.Offline);

            await userWalletGrain.Connect(new ConnectUserCommand(new List<UtxoAsset>()));
            var userWallet2 = await userWalletGrain.GetUserWallet();
            userWallet2.OnlineState.Should().Be(OnlineState.Online);
        }

        [Fact]
        public async Task ShouldBeOffLineWhenNoPingReplyHasBeenReceived()
        {
            var settingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(0);
            var settings = await settingsGrain.GetSettings();
            settings.UserWalletRoundTripPingInterval = TimeSpan.FromSeconds(2);
            settings.UserWalletOnlineIdleTimeout = TimeSpan.FromSeconds(4);
            await settingsGrain.UpdateSettings(settings);

            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_xxxx4");
            await userWalletGrain.Connect(new ConnectUserCommand(new List<UtxoAsset>()));

            await Task.Delay(TimeSpan.FromSeconds(3));
            await userWalletGrain.ReplyToClientPing(); //simulate ping
            var userWallet = await userWalletGrain.GetUserWallet();
            userWallet.OnlineState.Should().Be(OnlineState.Online);

            await Task.Delay(TimeSpan.FromSeconds(8));
            var userWallet2 = await userWalletGrain.GetUserWallet();
            userWallet2.OnlineState.Should().Be(OnlineState.Offline);
        }
    }

    [Collection(ClusterCollection.Name)]
    public class UpdateUserWalletStateCommandTests : OrleansTestBase
    {
        private IClusterClient Cluster { get; }

        public UpdateUserWalletStateCommandTests(ClusterFixture fixture) : base(fixture)
        {
            Cluster = fixture.ClusterClient;
        }
        
        [Fact]
        public async Task ShouldSucceedWhenUpdatingWalletState()
        {
            var userWalletGrain = Cluster.GetGrain<IUserWalletGrain>("stake_address_xxxx2");
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
            {
                new("abc", 1, "2a709b1d9c6a442317ee1032838415a48a3c66d6863e3ccf12bb48a02a709b1d9c6a442317ee1032838415a48a3c66d6863e3ccf12bb48a0", 1),
                new("abc", 1, "2a709b1d9c6a442317ee1032838415a48a3c66d6863e3ccf12bb48a02a709b1d9c6a442317ee1032838415a48a3c66d6863e3ccf12bb48a1", 2),
            }));

            var userWallet = await userWalletGrain.GetUserWallet();
            userWallet.AvailableUtxoAssets.Should().HaveCount(2);
        }
    }
    
    [Collection(ClusterCollection.Name)]
    public class GetStateRequests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        public GetStateRequests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
        }
        
        [Fact]
        public async Task ShouldSucceedWhenGettingStateForOrder()
        {
            var (userWalletGrain, orderGrain) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient);

            //UI will trigger signalR message to update wallet state
            var order = await orderGrain.GetOrder();
            var createOrderResponse = await orderGrain.GetOrder();
            var utxoAssets = new List<UtxoAsset>();
            foreach (var orderedPuzzlePiece in order.OrderedPuzzlePieces)
            {
                utxoAssets.Add(new UtxoAsset("blah", 1, orderedPuzzlePiece.BlockchainAssetId, 1));
            }
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(utxoAssets));

            var state = await userWalletGrain.GetActiveCompletedOrderPuzzlePieces(createOrderResponse.Id);
            state.ThrowIfNull();
            state.PuzzlePieces.Count().Should().BeGreaterThan(0);
            state.PuzzleDefinitions.Count.Should().Be(1);
        }

        [Fact]
        public async Task ShouldSucceedWhenGettingWalletState()
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
            walletState.PuzzleDefinitions.Count().Should().Be(1);
            walletState.PuzzlePieces.Count(x => x.IsOwned).Should().Be(1);
        }
    }
}