namespace Enigmi.Tests;

using Foundatio.Caching;
using Microsoft.Extensions.DependencyInjection;

public abstract class DbConnectedTests : Tests
{
	public DbConnectedTests()
	{
		//TestContext = ScopedServiceProvider.GetService<TestArtBankContext>()!;
		CacheClient = ScopedServiceProvider.GetService<ICacheClient>()!;
	}

	public ICacheClient CacheClient { get; }

	//public TestArtBankContext TestContext { get; }

	//public override async Task DisposeAsync()
	//{
	//	await TestContext.DisposeAsync().ContinueOnAnyContext();
	//}

	//public override async Task InitializeAsync()
	//{
	//	await TestContext.Database
	//		.ExecuteSqlRawAsync("exec tools.ResetTestDb @p0", parameters: new[] { "yes" }).ContinueOnAnyContext();
	//}
}