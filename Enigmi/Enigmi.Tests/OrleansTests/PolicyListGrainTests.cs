using System.Linq;
using System.Threading.Tasks;
using Enigmi.Grains.Shared.Policy;
using Enigmi.Grains.Shared.PolicyCollection;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;
using Xunit.Abstractions;
using static System.FormattableString;

namespace Enigmi.Tests.OrleansTests;

public class PolicyListGrainTests
{
    [Collection(ClusterCollection.Name)]
    public class SeedPuzzleCollectionCommandTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }
        public ITestOutputHelper TestOutputHelper { get; }

        public SeedPuzzleCollectionCommandTests(ClusterFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
            TestOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task ShouldSucceedWithPolicesBeingAddedToList()
        {
            var sniffer = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
            var seedPuzzleCollectionResponse = await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi3")));
            var createdPuzzleCollectionId = seedPuzzleCollectionResponse.Result!.PuzzleCollectionId;
            
            await TestUtil.Retry(5,30, async () =>
            {
                var puzzleCollectionGrain = ClusterClient.GetGrain<IPuzzleCollectionGrain>(createdPuzzleCollectionId);
                var puzzleDetail = await puzzleCollectionGrain.GetPuzzleCollection();
                if (puzzleDetail == null)
                {
                    return false;
                }
                
                return puzzleDetail!.PuzzlePolicy.PolicyId != null;
            });
            
            var grain = ClusterClient.GetGrain<IPolicyListGrain>(0);
            var policies = await grain.GetPolicies();
            policies.Count().Should().BeGreaterThan(0);
            var policyForCollection = policies.FirstOrDefault(x => x.PuzzleCollectionId == createdPuzzleCollectionId);
            policyForCollection.Should().NotBeNull();
            
            var policyGrain = ClusterClient.GetGrain<IPolicyGrain>(policyForCollection!.PolicyId);
            var policy = await policyGrain.GetPolicy();
            policy.PolicyClosingSlot.Should().BeGreaterThan(0);
        }
    }
}