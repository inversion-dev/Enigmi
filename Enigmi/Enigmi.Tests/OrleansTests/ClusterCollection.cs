using Xunit;

namespace Enigmi.Tests.OrleansTests;

[CollectionDefinition(Name)]
public class ClusterCollection : ICollectionFixture<ClusterFixture>
{
    public const string Name = "OrleansSilo";
}