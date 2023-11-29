using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardanoSharp.Wallet.CIPs.CIP14.Extensions;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Domain.Entities.OrderAggregate;
using Enigmi.Domain.Entities.PuzzlePieceAggregate;
using Enigmi.Grains.Shared.Order;
using Enigmi.Grains.Shared.PuzzleCollectionList;
using Enigmi.Grains.Shared.PuzzleCollectionList.Messages;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using Orleans;

namespace Enigmi.Tests.Utils;

public static class TestUtil
{
    public static async Task Retry(int delayInSeconds, int timeoutInSeconds, Func<Task<bool>> predicate)
    {
        var timesTried = 0;
        var timesToRetry = timeoutInSeconds / delayInSeconds;
        while ((!await predicate()) && timesTried < timesToRetry)
        {
            timesTried++;
            await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));
        }
    }

    public static UtxoAsset ConvertToUtxoAsset(PuzzlePiece x)
    {
        return new UtxoAsset(x.Id, 1, x.BlockchainAssetId, 1, x.BlockchainAssetId.ToAssetFingerprint());
    }

    public static HashSet<Guid> UnitTestSpecificPuzzleCollectionId { get; set; } = new();

    public static async Task<GetPuzzleCollectionsResponse.PuzzleCollectionDto> WaitForCollectionToBePublished(
        IClusterClient clusterClient, Guid? puzzleCollectionId = null)
    {
        GetPuzzleCollectionsResponse.PuzzleCollectionDto? puzzleCollectionDto = null;
        var puzzleCollectionListGrain = clusterClient.GetGrain<IPuzzleCollectionListGrain>(Constants.SingletonGrain);
        await TestUtil.Retry(5, 90, async () =>
        {
            var response = await puzzleCollectionListGrain.GetPuzzleCollections(new GetPuzzleCollectionsRequest());

            if (puzzleCollectionId.HasValue)
            {
                UnitTestSpecificPuzzleCollectionId.Add(puzzleCollectionId.Value);

                var specifiedCollection =
                    response!.Result!.PuzzleCollections.FirstOrDefault(x => x.Id == puzzleCollectionId);
                if (specifiedCollection != null)
                {
                    puzzleCollectionDto = specifiedCollection;
                    return true;
                }

                return false;
            }

            var collections = response!.Result!.PuzzleCollections
                .Where(x => !UnitTestSpecificPuzzleCollectionId.Contains(x.Id))
                .ToList();

            if (collections.Any())
            {
                puzzleCollectionDto = collections.First();
                return true;
            }

            return false;
        });

        puzzleCollectionDto.ThrowIfNull();
        return puzzleCollectionDto;
    }

    public static async Task<(IUserWalletGrain, IOrderGrain)> SimulatePlacingOrderAndWaitForOrderToComplete(
        IClusterClient clusterClient,
        string stakeAddress = "stake_address_xxxx4",
        int quantityToBuy = 1,
        Guid? puzzleCollectionId = null)
    {
        var paymentAddress =
            "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8";
        var userWalletGrain = clusterClient.GetGrain<IUserWalletGrain>(stakeAddress);

        var puzzleCollection = await TestUtil.WaitForCollectionToBePublished(clusterClient, puzzleCollectionId);

        await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(new List<UtxoAsset>
        {
            new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 1,
                Constants.LovelaceTokenAssetId, 100 * Constants.LovelacePerAda, string.Empty),
            new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 2,
                Constants.LovelaceTokenAssetId, 100 * Constants.LovelacePerAda, string.Empty),
            new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 3,
                Constants.LovelaceTokenAssetId, 100 * Constants.LovelacePerAda, string.Empty),
            new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 4,
                Constants.LovelaceTokenAssetId, 100 * Constants.LovelacePerAda, string.Empty),
            new UtxoAsset("29ff47baf100ae17e4e137bd79091eb0278e4225095a111ddef1de7fa67c3f6e", 5,
                Constants.LovelaceTokenAssetId, 100 * Constants.LovelacePerAda, string.Empty)
        }, paymentAddress));

        var createOrderResponse =
            await userWalletGrain.CreateOrder(new CreateOrderCommand(paymentAddress, puzzleCollection.Id, 4,
                quantityToBuy));

        if (createOrderResponse.HasErrors)
        {
            throw new Exception(createOrderResponse.Errors.First());
        }

        createOrderResponse.Result.ThrowIfNull();

        var cborWitness =
            "a1008382582017bdd84fd003a97bd52f161452fe3dacbb1b5e74fcf9cdbc2fcc6e709e3490da5840ce0f5d45ff780f5472b69a0e85b56136f8857b1a277130e8aefe51c5fe5861044db6a9c7f5d924f82a1ce0f171b10d5e4e7215207c9b26319b563f76e2b8710082582085cbf95d8edb070edc99ad1da058eaf3abc3bfa77801672cc64233146300d77b5840753b54007d833433747ea8ebc8b3bd43165428ae969f4f6b83c5d7507e065d166649c6414efd26bb8ed36da462e6832f9188e4c7a053cccf249c6e8253f28f0a825820c8095e97442b18db00eadcb4910bba7c954317891ebce975d5573eccd8c58c2958403fd6f394b7d6415e936a9ce2ffdab1d5ce04cef6f3ce2c1daaf3107e2dbd119a5c833f33f88f18f37717c6c1038eeedbc2fca51eee3eabd146afc8e1e458740b";

        var approveResponse =
            await userWalletGrain.ApproveOrder(
                new ApproveOrderCommand(createOrderResponse.Result!.OrderId, cborWitness));
        approveResponse.Result.ThrowIfNull();

        var orderGrain = clusterClient.GetGrain<IOrderGrain>(createOrderResponse.Result.OrderId);
        await TestUtil.Retry(3, 30, async () =>
        {
            var orderDetails = await orderGrain.GetOrder();
            return orderDetails.State == OrderState.Completed;
        });

        return (userWalletGrain, orderGrain);
    }
}