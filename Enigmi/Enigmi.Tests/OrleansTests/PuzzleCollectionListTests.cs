using System.Linq;
using System.Threading.Tasks;
using Enigmi.Domain.ValueObjects;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleCollectionList;
using Enigmi.Grains.Shared.PuzzleCollectionList.Messages;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;

namespace Enigmi.Tests.OrleansTests;

public class PuzzleCollectionListTests
{
    [Collection(ClusterCollection.Name)]
    public class GetPuzzleCollectionsRequestTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        public GetPuzzleCollectionsRequestTests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldSucceedWhenGettingPuzzleCollections()
        {
            await TestUtil.WaitForCollectionToBePublished(ClusterClient);
            
            var puzzleCollectionListGrain = ClusterClient.GetGrain<IPuzzleCollectionListGrain>(0);
            var puzzleCollectionResponse = await puzzleCollectionListGrain.GetPuzzleCollections(new GetPuzzleCollectionsRequest());
            puzzleCollectionResponse.Result!.PuzzleCollections.Count().Should().BeGreaterThan(0);

            var puzzleCollectionId =
                puzzleCollectionResponse.Result!.PuzzleCollections.Select(x => x.Id).FirstOrDefault();

            var puzzleCollectionGrain = ClusterClient.GetGrain<IPuzzleCollectionGrain>(puzzleCollectionId);
            var puzzleCollection = puzzleCollectionGrain.GetPuzzleCollection();
            puzzleCollection!.Result!.Status.Should().Be(ActivationStatus.Published);
        }        
    }
}