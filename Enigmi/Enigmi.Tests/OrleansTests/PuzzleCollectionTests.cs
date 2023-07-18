using System.Linq;
using System.Threading.Tasks;
using Enigmi.Common;
using Enigmi.Grains.Shared.PuzzleCollection;
using Enigmi.Grains.Shared.PuzzleCollectionList;
using Enigmi.Grains.Shared.PuzzleCollectionList.Messages;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;
using Enigmi.Tests.Utils;
using FluentAssertions;
using Orleans;
using Xunit;
using static System.FormattableString;

namespace Enigmi.Tests.OrleansTests;

public class PuzzleCollectionTests
{
    [Collection(ClusterCollection.Name)]
    public class SeedPuzzleCollectionCommandTests : OrleansTestBase
    {
        private IClusterClient ClusterClient { get; }

        public SeedPuzzleCollectionCommandTests(ClusterFixture fixture) : base(fixture)
        {
            ClusterClient = fixture.ClusterClient;
        }

        [Fact]
        public async Task ShouldSucceedWhenSeedingPuzzle()
        {
            var sniffer =
                ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
            var puzzleCollectionListGrain = ClusterClient.GetGrain<IPuzzleCollectionListGrain>(0);
            var seedPuzzleCollectionResponse =
                await sniffer.SeedPuzzleCollection(new SeedPuzzleCollectionCommand(Invariant($"/drops/Enigmi2")));
            var result = seedPuzzleCollectionResponse.Result;
            result.ThrowIfNull();

            var testUtility = new TestUtility($"{Setting.EnvironmentPrefix.ToLowerInvariant()}");
            var blobNames = await testUtility.GetAllBlobNames();
            var domainBlobs = blobNames
                .Where(x => x.StartsWith(Invariant($"domain/PuzzleCollection/{result.PuzzleCollectionId}/"))).ToList();
            domainBlobs.Any(x => x.Contains("normalized.jpg"));
            domainBlobs.Count(x => x.Contains("/PuzzlePieceDefinition/")).Should()
                .Be(4, "Expecting 4 puzzle piece definition images to be uploaded");

            var puzzleCollectionResponse =
                await puzzleCollectionListGrain.GetPuzzleCollections(new GetPuzzleCollectionsRequest());
            var puzzleCollection = puzzleCollectionResponse.Result!.PuzzleCollections.FirstOrDefault();
            puzzleCollection.Should().NotBeNull();

            await TestUtil.Retry(5, 30, async () =>
            {
                var puzzleCollectionGrain = ClusterClient.GetGrain<IPuzzleCollectionGrain>(puzzleCollection!.Id);
                var puzzleCollectionDetail = await puzzleCollectionGrain.GetPuzzleCollection();
                puzzleCollectionDetail.Should().NotBeNull();
                puzzleCollectionDetail!.PermittedPuzzleSize.Count().Should().Be(1);

                if (puzzleCollectionDetail.PuzzlePolicy.PolicyId != null
                    && puzzleCollectionDetail.PuzzlePiecePolicy.PolicyId != null)
                {
                    return true;
                }

                return false;
            });

            var puzzleCollectionGrain = ClusterClient.GetGrain<IPuzzleCollectionGrain>(puzzleCollection!.Id);
            var puzzleCollectionDetail = await puzzleCollectionGrain.GetPuzzleCollection();
            puzzleCollectionDetail.Should().NotBeNull();
            puzzleCollectionDetail!.PuzzlePolicy.PolicyId.Should().NotBeNull();
            puzzleCollectionDetail!.PuzzlePiecePolicy.PolicyId.Should().NotBeNull();
        }
    }
}