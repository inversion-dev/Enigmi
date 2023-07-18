using System;
using System.Threading.Tasks;
using Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate;
using Enigmi.Grains.Shared.BlockchainTransactionSubmission;
using Enigmi.Grains.Shared.GrainSettings;
using FluentAssertions;
using Orleans;
using Orleans.TestingHost;
using Xunit;

namespace Enigmi.Tests.OrleansTests;

public class BlockchainSubmissionGrainTests
{
    [Collection(ClusterCollection.Name)]
    public class SubmitTests : OrleansTestBase
    {
        private ClusterFixture Fixture { get; }

        private IClusterClient ClusterClient { get; }

        public SubmitTests(ClusterFixture fixture) : base(fixture)
        {
            Fixture = fixture;
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldSucceedWithStateOfCompletedOrder()
        {
            Guid orderId = Guid.NewGuid();
            var orderGrain = ClusterClient.GetGrain<IBlockchainTransactionSubmissionGrain>(orderId);
            var submitResponse = await orderGrain.Submit(orderId, "success", DateTime.UtcNow.AddSeconds(90));
            await Task.Delay(TimeSpan.FromSeconds(22));
            var submissionDetails = await orderGrain.GetBlockchainTransactionSubmissionDetail();
            submissionDetails.State.Should().Be(BlockchainTransactionProcessState.OnChainConfirmed);
        }
        
        [Fact]
        public async Task ShouldSucceedWithStateOfOnChain()
        {
            Guid orderId = Guid.NewGuid();
            var orderGrain = ClusterClient.GetGrain<IBlockchainTransactionSubmissionGrain>(orderId);
            var submitResponse = await orderGrain.Submit(orderId, "successOnChain", DateTime.UtcNow.AddSeconds(90));
            await Task.Delay(TimeSpan.FromSeconds(22));
            var submissionDetails = await orderGrain.GetBlockchainTransactionSubmissionDetail();
            submissionDetails.State.Should().Be(BlockchainTransactionProcessState.OnChainConfirmed);
        }
        
        [Fact]
        public async Task ShouldFailWithStateOfTransientRejected()
        {
            var grainSettings = ClusterClient.GetGrain<IGrainSettingsGrain>(0);
            var settings = await grainSettings.GetSettings();
            settings.OrderBlockchainTransactionSettings.MaxTransientRejectedCount = 3;
            await grainSettings.UpdateSettings(settings);
            
            Guid orderId = Guid.NewGuid();
            var orderGrain = ClusterClient.GetGrain<IBlockchainTransactionSubmissionGrain>(orderId);
            var submitResponse = await orderGrain.Submit(orderId, "reject", DateTime.UtcNow.AddSeconds(90));
            await Task.Delay(TimeSpan.FromSeconds(7));
            var submissionDetails = await orderGrain.GetBlockchainTransactionSubmissionDetail();
            submissionDetails.State.Should().Be(BlockchainTransactionProcessState.TransientRejected);
        }
        
        [Fact]
        public async Task ShouldFailWithStateOfRejected()
        {
            Guid orderId = Guid.NewGuid();
            
            var grainSettings = ClusterClient.GetGrain<IGrainSettingsGrain>(0);
            var settings = await grainSettings.GetSettings();
            settings.OrderBlockchainTransactionSettings.MaxTransientRejectedCount = 1;
            await grainSettings.UpdateSettings(settings);
            
            var orderGrain = ClusterClient.GetGrain<IBlockchainTransactionSubmissionGrain>(orderId);
            var submitResponse = await orderGrain.Submit(orderId, "reject", DateTime.UtcNow.AddSeconds(90));
            await Task.Delay(TimeSpan.FromSeconds(11 * 1));
            var submissionDetails = await orderGrain.GetBlockchainTransactionSubmissionDetail();
            submissionDetails.State.Should().Be(BlockchainTransactionProcessState.Rejected);
        }
    }
}