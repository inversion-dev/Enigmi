using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Domain.Entities.OrderAggregate;
using Enigmi.Domain.Entities.PuzzlePieceDispenserAggregate;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.Order;
using Enigmi.Grains.Shared.PuzzleCollectionList.Messages;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.PuzzlePieceDispenser;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;
using static System.FormattableString;

namespace Enigmi.Tests.OrleansTests;

public class OrderGrainTests 
{
    [Collection(ClusterCollection.Name)]
    public class BuildOrderTests : OrleansTestBase
    {
        private ClusterFixture Fixture { get; }
        private IClusterClient ClusterClient { get; }

        public BuildOrderTests(ClusterFixture fixture) : base(fixture)
        {
            Fixture = fixture;
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldFailWhenInsufficientFunds()
        {
            var (puzzleCollection, puzzleSize) = await GetPuzzleCollectionDetails();
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_order1");

            //add funds to user wallet
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
            {
                new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 1,
                    Constants.LovelaceTokenAssetId, Convert.ToUInt64(0.5 * Constants.LovelacePerAda))
            }, "aaa"));

            var command = new CreateOrderCommand(
                "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8"
                , puzzleCollection.Id
                , puzzleSize
                , 1);

            var createOrderResponse = await userWalletGrain.CreateOrder(command);
            createOrderResponse.Errors.First().Should().Be("UTxOs have insufficient balance");
        }
        
        [Fact]
        public async Task ShouldSucceedByExpiringStaleOrder()
        {
            var (puzzleCollection, puzzleSize) = await GetPuzzleCollectionDetails();
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_order1");

            var grainSettingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
            var settings = await grainSettingsGrain.GetSettings();
            settings.OrderGrain.OrderExpiresTimespan = TimeSpan.FromSeconds(2);
            await grainSettingsGrain.UpdateSettings(settings);

            //add funds to user wallet
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
            {
                new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 1,
                    Constants.LovelaceTokenAssetId, Convert.ToUInt64(10 * Constants.LovelacePerAda))
            }, "aaa"));

            var command = new CreateOrderCommand(
                "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8"
                , puzzleCollection.Id
                , puzzleSize
                , 1);

            var createOrderResponse = await userWalletGrain.CreateOrder(command);
            var orderId = createOrderResponse!.Result!.OrderId;
            
            var dispenserGrain = ClusterClient.GetGrain<IPuzzlePieceDispenserGrain>(PuzzlePieceDispenser.GetId(puzzleCollection.Id, puzzleSize));
            
            var userWalletBefore = await userWalletGrain.GetUserWallet();
            var isUtxoReservedAgainstOrderBefore = userWalletBefore.UtxoReservations.Any(x => x.ReserverId == orderId);
            isUtxoReservedAgainstOrderBefore.Should().BeTrue();

            var puzzlePieceDispenserBefore = await dispenserGrain.GetPuzzlePieceDispenser();
            puzzlePieceDispenserBefore.Reservations.Any(x => x.Id == orderId).Should().BeTrue();

            var orderGrain = ClusterClient.GetGrain<IOrderGrain>(orderId);
            await TestUtil.Retry(2,10, async () =>
            {
                var placedOrder = await orderGrain.GetOrder();
                return placedOrder.State == OrderState.Cancelled;
            });

            //ensure order is cancelled
            var placedOrder = await orderGrain.GetOrder();
            placedOrder.State.Should().Be(OrderState.Cancelled);
            
            await TestUtil.Retry(2, 10, async () =>
            {
                var userWallet = await userWalletGrain.GetUserWallet();
                return !userWallet.UtxoReservations.Any(x => x.ReserverId == orderId);
            });

            //ensure reserved utxo's has been released
            var userWalletAfter = await userWalletGrain.GetUserWallet();
            var isUtxoReservedAgainstOrder = userWalletAfter.UtxoReservations.Any(x => x.ReserverId == orderId);
            isUtxoReservedAgainstOrder.Should().BeFalse();
            
            //dispenser should have removed reserved items
            var puzzlePieceDispenserAfter = await dispenserGrain.GetPuzzlePieceDispenser();
            puzzlePieceDispenserAfter.Reservations.Any(x => x.Id == orderId).Should().BeFalse();
        }
        
        [Fact]
        public async Task ShouldSucceedByAutomaticallyRemovingStaleReservedInDispenser()
        {
            var (puzzleCollection, puzzleSize) = await GetPuzzleCollectionDetails();
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_order1");

            //add funds to user wallet
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
            {
                new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 1,
                    Constants.LovelaceTokenAssetId, Convert.ToUInt64(10 * Constants.LovelacePerAda))
            },"aaa"));

            var command = new CreateOrderCommand(
                "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8"
                , puzzleCollection.Id
                , puzzleSize
                , 1);

            var createOrderResponse = await userWalletGrain.CreateOrder(command);
            var orderId = createOrderResponse!.Result!.OrderId;

            var dispenserGrain = ClusterClient.GetGrain<IPuzzlePieceDispenserGrain>(PuzzlePieceDispenser.GetId(puzzleCollection.Id, puzzleSize));
            
            try
            {
                await dispenserGrain.UpdateDispenserExpiresTimespan(TimeSpan.FromSeconds(1));

                var puzzlePieceDispenserBefore = await dispenserGrain.GetPuzzlePieceDispenser();
                puzzlePieceDispenserBefore.Reservations.Any(x => x.Id == orderId).Should().BeTrue();

                await TestUtil.Retry(2, 10, async () =>
                {
                    var puzzlePieceDispenser = await dispenserGrain.GetPuzzlePieceDispenser();
                    return !puzzlePieceDispenser.Reservations.Any(x => x.Id == orderId);
                });

                //dispenser should have removed reserved items
                var puzzlePieceDispenserAfter = await dispenserGrain.GetPuzzlePieceDispenser();
                puzzlePieceDispenserAfter.Reservations.Any(x => x.Id == orderId).Should().BeFalse();
            }
            finally
            {
                await dispenserGrain.UpdateDispenserExpiresTimespan(TimeSpan.FromMinutes(6));    
            }
        }

        private async Task<(GetPuzzleCollectionsResponse.PuzzleCollectionDto puzzleCollection, int puzzleSize)> GetPuzzleCollectionDetails()
        {
            var puzzleCollectionResult = await TestUtil.WaitForCollectionToBePublished(ClusterClient);
            puzzleCollectionResult.ThrowIfNull();
            return (puzzleCollectionResult, puzzleCollectionResult.PermittedPuzzleSize.First());
        }

        [Fact]
        public async Task ShouldSucceedWhenPlacingOrdersWithEnoughFunds()
        {
            var (puzzleCollection, puzzleSize) = await GetPuzzleCollectionDetails();
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_order2");

            //add funds to user wallet
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
            {
                new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 1,
                    Constants.LovelaceTokenAssetId, 10 * Constants.LovelacePerAda)
            }, "aaa"));

            var command = new CreateOrderCommand(
                "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8"
                , puzzleCollection.Id
                , puzzleSize
                , 1);

            var createOrderResponse = await userWalletGrain.CreateOrder(command);

            createOrderResponse.HasErrors.Should().Be(false);

            var createOrderResponseResult = createOrderResponse.Result.ThrowIfNull();
            
            var cborWitness =
                "a1008382582017bdd84fd003a97bd52f161452fe3dacbb1b5e74fcf9cdbc2fcc6e709e3490da5840ce0f5d45ff780f5472b69a0e85b56136f8857b1a277130e8aefe51c5fe5861044db6a9c7f5d924f82a1ce0f171b10d5e4e7215207c9b26319b563f76e2b8710082582085cbf95d8edb070edc99ad1da058eaf3abc3bfa77801672cc64233146300d77b5840753b54007d833433747ea8ebc8b3bd43165428ae969f4f6b83c5d7507e065d166649c6414efd26bb8ed36da462e6832f9188e4c7a053cccf249c6e8253f28f0a825820c8095e97442b18db00eadcb4910bba7c954317891ebce975d5573eccd8c58c2958403fd6f394b7d6415e936a9ce2ffdab1d5ce04cef6f3ce2c1daaf3107e2dbd119a5c833f33f88f18f37717c6c1038eeedbc2fca51eee3eabd146afc8e1e458740b";

            //approve order
            var approveResponse =
                await userWalletGrain.ApproveOrder(new ApproveOrderCommand(createOrderResponseResult.OrderId,
                    cborWitness));
        }
        
        [Fact]
        public async Task ShouldSucceedAndCopyPlaceHolderImage()
        {
            var (puzzleCollection, puzzleSize) = await GetPuzzleCollectionDetails();
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_order2");

            //add funds to user wallet
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
            {
                new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 1,
                    Constants.LovelaceTokenAssetId, 10 * Constants.LovelacePerAda)
            }, "aaa"));

            var command = new CreateOrderCommand(
                "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8"
                , puzzleCollection.Id
                , puzzleSize
                , 1);

            var createOrderResponse = await userWalletGrain.CreateOrder(command);

            createOrderResponse.HasErrors.Should().Be(false);

            var createOrderResponseResult = createOrderResponse.Result.ThrowIfNull();
            var placeOrderId = createOrderResponseResult.OrderId;
            
            
            var orderGrain = this.ClusterClient.GetGrain<IOrderGrain>(placeOrderId);
            var order = await orderGrain.GetOrder();
            var puzzlePieceId = order.OrderedPuzzlePieces.Select(x => x.Id).First();
            
            var puzzlePieceGrain = this.ClusterClient.GetGrain<IPuzzlePieceGrain>(puzzlePieceId);
            var puzzlePiece = await puzzlePieceGrain.GetPuzzlePiece();
            puzzlePiece.ThrowIfNull();
            
            //check blob storage
            var testUtility = new TestUtility(Invariant($"{this.Setting.EnvironmentPrefix.ToLowerInvariant()}"));
            var blobNames = await testUtility.GetAllBlobNames();

            var blobEntry = blobNames.SingleOrDefault(x => Invariant($"/{x}").Equals(puzzlePiece.BlobImagePath, StringComparison.InvariantCulture));
            blobEntry.Should().NotBeNull();

            var tempImageBytes = await testUtility.DownloadBlobBytes(blobEntry!);
            
            var cborWitness =
                "a1008382582017bdd84fd003a97bd52f161452fe3dacbb1b5e74fcf9cdbc2fcc6e709e3490da5840ce0f5d45ff780f5472b69a0e85b56136f8857b1a277130e8aefe51c5fe5861044db6a9c7f5d924f82a1ce0f171b10d5e4e7215207c9b26319b563f76e2b8710082582085cbf95d8edb070edc99ad1da058eaf3abc3bfa77801672cc64233146300d77b5840753b54007d833433747ea8ebc8b3bd43165428ae969f4f6b83c5d7507e065d166649c6414efd26bb8ed36da462e6832f9188e4c7a053cccf249c6e8253f28f0a825820c8095e97442b18db00eadcb4910bba7c954317891ebce975d5573eccd8c58c2958403fd6f394b7d6415e936a9ce2ffdab1d5ce04cef6f3ce2c1daaf3107e2dbd119a5c833f33f88f18f37717c6c1038eeedbc2fca51eee3eabd146afc8e1e458740b";

            //approve order
            var approveResponse =
                await userWalletGrain.ApproveOrder(new ApproveOrderCommand(placeOrderId,
                    cborWitness));
            
            await TestUtil.Retry(3, 30, async () =>
            {
                var orderDetails = await orderGrain.GetOrder();
                return orderDetails.State == OrderState.Completed;
            });

            var orderDetails = await orderGrain.GetOrder();
            orderDetails.State.Should().Be(OrderState.Completed);
            
            //should be replaced by new image when order is completed
            var tempImageBytes2 = await testUtility.DownloadBlobBytes(blobEntry!);
            var isSameImage = tempImageBytes.SequenceEqual(tempImageBytes2);
            isSameImage.Should().BeFalse();
        }
        
        [Fact]
        public async Task ShouldSucceedWithCompletedOrder()
        {
            var (puzzleCollection, puzzleSize) = await GetPuzzleCollectionDetails();
            var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>("stake_address_order3");

            //add funds to user wallet
            await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
            {
                new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 1,
                    Constants.LovelaceTokenAssetId, 10 * Constants.LovelacePerAda)
            }, "aaa"));

            var command = new CreateOrderCommand(
                "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8"
                , puzzleCollection.Id
                , puzzleSize
                , 1);

            var createOrderResponse = await userWalletGrain.CreateOrder(command);

            if (createOrderResponse.HasErrors)
            {
                throw new Exception(createOrderResponse.Errors.First());
            }

            var createOrderResponseResult = createOrderResponse.Result.ThrowIfNull();
            
            var cborWitness =
                "a1008382582017bdd84fd003a97bd52f161452fe3dacbb1b5e74fcf9cdbc2fcc6e709e3490da5840ce0f5d45ff780f5472b69a0e85b56136f8857b1a277130e8aefe51c5fe5861044db6a9c7f5d924f82a1ce0f171b10d5e4e7215207c9b26319b563f76e2b8710082582085cbf95d8edb070edc99ad1da058eaf3abc3bfa77801672cc64233146300d77b5840753b54007d833433747ea8ebc8b3bd43165428ae969f4f6b83c5d7507e065d166649c6414efd26bb8ed36da462e6832f9188e4c7a053cccf249c6e8253f28f0a825820c8095e97442b18db00eadcb4910bba7c954317891ebce975d5573eccd8c58c2958403fd6f394b7d6415e936a9ce2ffdab1d5ce04cef6f3ce2c1daaf3107e2dbd119a5c833f33f88f18f37717c6c1038eeedbc2fca51eee3eabd146afc8e1e458740b";

            //approve order
            var approveResponse =
                await userWalletGrain.ApproveOrder(new ApproveOrderCommand(createOrderResponseResult.OrderId,
                    cborWitness));

            approveResponse.HasErrors.Should().Be(false);
            
            var orderGrain = ClusterClient.GetGrain<IOrderGrain>(createOrderResponse.Result.OrderId);
            await TestUtil.Retry(3, 30, async () =>
            {
                var orderDetails = await orderGrain.GetOrder();
                return orderDetails.State == OrderState.Completed;
            });

            var orderDetails = await orderGrain.GetOrder();
            orderDetails.State.Should().Be(OrderState.Completed);
        }
    }
}