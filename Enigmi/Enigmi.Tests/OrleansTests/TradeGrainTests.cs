using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.ValueObjects;
using Enigmi.Common;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Grains.Shared.PuzzlePiece;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWalletActiveTradeList;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;
using Enigmi.Domain.Entities.PuzzlePieceAggregate;
using Enigmi.Domain.ValueObjects;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;
using Enigmi.Grains.Shared.Trade.Messages;
using Enigmi.Grains.Shared.UserWallet.Messages;
using static System.FormattableString;

namespace Enigmi.Tests.OrleansTests;

public class TradeGrainTests
{
    [Collection(ClusterCollection.Name)]
    public class BuildTransactionCommandTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        public BuildTransactionCommandTests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldFailWhenNotEnoughFundsAreAvailable()
        {
            var sniffer = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
            var seedPuzzleCollectionResponse = await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi8")));
            
            var stakeAddress1 = "stake_address_xxxx4.1";
            var (userWalletGrain1, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient, stakeAddress1, quantityToBuy: 2, seedPuzzleCollectionResponse.Result!.PuzzleCollectionId);

            var stakeAddress2 = "stake_address_xxxx5.1";
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
            await userWalletGrain2.Connect(new ConnectUserCommand(secondItem, "abc", "bbb"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var trades = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, firstPuzzle.PuzzlePieceDefinitionId);
            var tradeDetail = trades.UserWalletTradeDetails.First(x => x.StakingAddress == stakeAddress2).TradeDetails.First();
            
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
                
                return activeTrades.Any(x => tradeDetail.InitiatingPiece.PuzzlePieceId == x.TradeDetail.InitiatingPiece.PuzzlePieceId);
            });

            await TestUtil.Retry(2, 25, async () =>
            {
                await userWalletGrain1.PingUser();
                await userWalletGrain2.PingUser();
                
                var activeTrades = await userWalletActiveTradeListGrain2.GetActiveTrades();
                return activeTrades.Any(x => tradeDetail.InitiatingPiece.PuzzlePieceId == x.TradeDetail.InitiatingPiece.PuzzlePieceId);
            });

            var activeTrades2 = await userWalletActiveTradeListGrain2.GetActiveTrades();
            var trade = activeTrades2.FirstOrDefault(x =>
                x.TradeDetail.InitiatingPiece.PuzzlePieceId == tradeDetail.InitiatingPiece.PuzzlePieceId);

            var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(trade!.Id);
            var retrievedTrade = await tradeGrain.GetTrade();
            retrievedTrade.State.Should().Be(TradeState.New);
            
            var response = await tradeGrain.BuildTransaction(new CreateTransactionCommand());
            response.HasErrors.Should().BeTrue();
            response.Errors.First().Should().Be("Not enough funds to complete the transaction");
        }      
        
        [Fact]
        public async Task ShouldSucceedWhenBuildingTransaction()
        {
            var sniffer = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
            var seedPuzzleCollectionResponse = await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi5")));
            
            var stakeAddress1 = "stake_address_xxxx4T";
            var (userWalletGrain1, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient, stakeAddress1, quantityToBuy: 2, seedPuzzleCollectionResponse.Result!.PuzzleCollectionId);

            var stakeAddress2 = "stake_address_xxxx5T";
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

            var firstUtxo = firstItem.First();
            firstItem = firstItem.Union(new List<UtxoAsset>
            {
                new UtxoAsset(firstUtxo.TxId, firstUtxo.OutputIndexOnTx, Constants.LovelaceTokenAssetId, 5 * Constants.LovelacePerAda, string.Empty)
            }).ToList();
            
            var secondUtxo = secondItem.First();
            secondItem = secondItem.Union(new List<UtxoAsset>
            {
                new UtxoAsset(secondUtxo.TxId, secondUtxo.OutputIndexOnTx, Constants.LovelaceTokenAssetId, 5 * Constants.LovelacePerAda, string.Empty)
            }).ToList();


            await userWalletGrain1.Connect(new ConnectUserCommand(firstItem, "abc", "aaa"));
            await userWalletGrain2.Connect(new ConnectUserCommand(secondItem, "abc", "bbb"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var trades = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, firstPuzzle.PuzzlePieceDefinitionId);
            var tradeDetail = trades.UserWalletTradeDetails.First(x => x.StakingAddress == stakeAddress2).TradeDetails.First();
            
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
            
            var response = await tradeGrain.BuildTransaction(new CreateTransactionCommand());
            if (response.HasErrors)
            {
                throw new Exception(response.Errors.First());
            }
            
            var retrievedTradeAfterTransactionBuild = await tradeGrain.GetTrade();
            retrievedTradeAfterTransactionBuild.BlockchainTransaction.Should().NotBeNull();
            retrievedTradeAfterTransactionBuild.BlockchainTransaction!.TransactionId.Should().NotBeNull();
            retrievedTradeAfterTransactionBuild.BlockchainTransaction!.UnsignedTransactionCborHex.Should().NotBeNull();
            retrievedTradeAfterTransactionBuild.BlockchainTransaction!.Fee.Should().NotBeNull();
        }       
    }
    
    [Collection(ClusterCollection.Name)]
    public class SignTradeByCounterpartCommandTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        public SignTradeByCounterpartCommandTests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldSucceedSignTradeByCounterpartyCommand()
        {
            var sniffer = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
            var seedPuzzleCollectionResponse = await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi6")));
            
            var stakeAddress1 = "stake_address_xxxx4.3";
            var (userWalletGrain1, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient, stakeAddress1, quantityToBuy: 2, seedPuzzleCollectionResponse.Result!.PuzzleCollectionId);

            var stakeAddress2 = "stake_address_xxxx5.3";
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

            var firstUtxo = firstItem.First();
            firstItem = firstItem.Union(new List<UtxoAsset>
            {
                new UtxoAsset(firstUtxo.TxId, firstUtxo.OutputIndexOnTx, Constants.LovelaceTokenAssetId, 5 * Constants.LovelacePerAda, string.Empty)
            }).ToList();
            
            var secondUtxo = secondItem.First();
            secondItem = secondItem.Union(new List<UtxoAsset>
            {
                new UtxoAsset(secondUtxo.TxId, secondUtxo.OutputIndexOnTx, Constants.LovelaceTokenAssetId, 5 * Constants.LovelacePerAda, string.Empty)
            }).ToList();
            
            await userWalletGrain1.Connect(new ConnectUserCommand(firstItem, "abc", "aaa"));
            await userWalletGrain2.Connect(new ConnectUserCommand(secondItem, "abc", "bbb"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var trades = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, firstPuzzle.PuzzlePieceDefinitionId);
            var tradeDetail = trades.UserWalletTradeDetails.First(x => x.StakingAddress == stakeAddress2).TradeDetails.First();
            
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

            await TestUtil.Retry(2, 25, async () =>
            {
                await userWalletGrain1.PingUser();
                await userWalletGrain2.PingUser();
                
                var activeTrades = await userWalletActiveTradeListGrain2.GetActiveTrades();
                return activeTrades.Any();
            });

            var activeTrades2 = await userWalletActiveTradeListGrain2.GetActiveTrades();
            var trade = activeTrades2.First();

            var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(trade.Id);
            var retrievedTrade = await tradeGrain.GetTrade();
            retrievedTrade.State.Should().Be(TradeState.New);
            
            var response = await tradeGrain.BuildTransaction(new CreateTransactionCommand());
            if (response.HasErrors)
            {
                throw new Exception(response.Errors.First());
            }
            
            var signTradeByCounterparty = await tradeGrain.SignByCounterparty(new SignTradeByCounterpartyCommand("kdlka908023984029023223234"));
            if (signTradeByCounterparty.HasErrors)
            {
                throw new Exception(signTradeByCounterparty.Errors.First());
            }
            
            var retrievedTradeAfterSignByCounterParty = await tradeGrain.GetTrade();
            retrievedTradeAfterSignByCounterParty.State.Should().Be(TradeState.CounterpartySigned);
        }
    }
    
    [Collection(ClusterCollection.Name)]
    public class SignTradeByInitiatingPartyCommandTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        public SignTradeByInitiatingPartyCommandTests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldSucceedSignTradeByInitiatingParty()
        {
            var sniffer = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
            var seedPuzzleCollectionResponse = await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi7")));
            
            var stakeAddress1 = "stake_address_xxxxsgn4.3";
            var (userWalletGrain1, orderGrain1) = await TestUtil.SimulatePlacingOrderAndWaitForOrderToComplete(ClusterClient, stakeAddress1, quantityToBuy: 2, seedPuzzleCollectionResponse.Result!.PuzzleCollectionId);

            var stakeAddress2 = "stake_address_xxxxsgn5.3";
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

            var firstUtxo = firstItem.First();
            firstItem = firstItem.Union(new List<UtxoAsset>
            {
                new UtxoAsset(firstUtxo.TxId, firstUtxo.OutputIndexOnTx, Constants.LovelaceTokenAssetId, 5 * Constants.LovelacePerAda, string.Empty)
            }).ToList();
            
            var secondUtxo = secondItem.First();
            secondItem = secondItem.Union(new List<UtxoAsset>
            {
                new UtxoAsset(secondUtxo.TxId, secondUtxo.OutputIndexOnTx, Constants.LovelaceTokenAssetId, 5 * Constants.LovelacePerAda, string.Empty)
            }).ToList();
            
            await userWalletGrain1.Connect(new ConnectUserCommand(firstItem, "abc", "addr_test1qpvmvsa2lqvl3y79rl9qm87e03uf48u7s4ecpenluppqvv73nadmqahcncugv7qfxlnmneyw4alky6w9kg0setjkpeuqz297u8"));
            await userWalletGrain2.Connect(new ConnectUserCommand(secondItem, "abc", "addr1q8226duy0zfx4u3pvldwra8z3xwrptq2y080szn3r34rercfe5v9a840zta3vjpxclxuu957lsqgynp2zpmdmgufjr2s5jg8td"));

            var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
            var trades = await activePuzzlePieceListGrain.FindPotentialTrades(stakeAddress1, firstPuzzle.PuzzlePieceDefinitionId);
            var tradeDetail = trades.UserWalletTradeDetails.First(x => x.StakingAddress == stakeAddress2).TradeDetails.First();
            
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

            await TestUtil.Retry(2, 25, async () =>
            {
                await userWalletGrain1.PingUser();
                await userWalletGrain2.PingUser();
                
                var activeTrades = await userWalletActiveTradeListGrain2.GetActiveTrades();
                return activeTrades.Any();
            });
            
            var activeTrades2 = await userWalletActiveTradeListGrain2.GetActiveTrades();
            var trade = activeTrades2.First();

            var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(trade.Id);
            var retrievedTrade = await tradeGrain.GetTrade();
            retrievedTrade.State.Should().Be(TradeState.New);
            
            var response = await tradeGrain.BuildTransaction(new CreateTransactionCommand());
            if (response.HasErrors)
            {
                throw new Exception(response.Errors.First());
            }
            
            var signTradeByCounterparty = await tradeGrain.SignByCounterparty(new SignTradeByCounterpartyCommand("a10081825820bd1f24411e158cca8fb655dddd34a7099fe5fc667522d510549a01cdb79181c45840740f062d642014fafb7c328f8a1c0c8b6570b7a47d11d0e20e848ba20ac6308cc12228b8cee3a883438b25483c47305b631dcec567d838cc62aef67337cebd0f"));
            if (signTradeByCounterparty.HasErrors)
            {
                throw new Exception(signTradeByCounterparty.Errors.First());
            }
            
            //wait for reservations to be made
            await TestUtil.Retry(2, 25, async () =>
            {
                await userWalletGrain1.PingUser();
                await userWalletGrain2.PingUser();
                
                var wallet1 = await userWalletGrain1.GetUserWallet();
                var wallet2 = await userWalletGrain2.GetUserWallet();
                return wallet1.UtxoReservations.Any() && wallet2.UtxoReservations.Any();
            });
            
            var retrievedTradeAfterSignByCounterParty = await tradeGrain.GetTrade();
            retrievedTradeAfterSignByCounterParty.State.Should().Be(TradeState.CounterpartySigned);

            var signTradeByInitiatingPartyResponse = await tradeGrain.SignTradeByInitiatingParty(new SignTradeByInitiatingPartyCommand("a10081825820bd1f24411e158cca8fb655dddd34a7099fe5fc667522d510549a01cdb79181c45840740f062d642014fafb7c328f8a1c0c8b6570b7a47d11d0e20e848ba20ac6308cc12228b8cee3a883438b25483c47305b631dcec567d838cc62aef67337cebd0f"));
            if (signTradeByInitiatingPartyResponse.HasErrors)
            {
                throw new Exception(signTradeByInitiatingPartyResponse.Errors.First());
            }
            
            var tradeAfterInitiatingPartySigning = await tradeGrain.GetTrade();
            tradeAfterInitiatingPartySigning.State.Should().Be(TradeState.FullySigned);
            
            await TestUtil.Retry(3, 30, async () =>
            {
                var orderDetails = await tradeGrain.GetTrade();
                return orderDetails.State == TradeState.Completed;
            });

            var tradeAfterSubmittedToBlockchain = await tradeGrain.GetTrade();
            tradeAfterSubmittedToBlockchain.State.Should().Be(TradeState.Completed);
        }
    }
}