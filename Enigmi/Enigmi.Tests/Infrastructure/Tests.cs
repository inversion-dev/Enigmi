using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Enigmi.Tests;

public abstract class Tests : IAsyncLifetime
{
	static Tests()
	{
	}

	public Tests()
	{
		var services = new ServiceCollection()
		   .ConfigureTestServices();

		services = OverrideMocks(services);

		ServiceProvider = services.BuildServiceProvider();

		ServiceScope = ServiceProvider.CreateScope();
		ScopedServiceProvider = ServiceScope.ServiceProvider;
	}

	protected DataBuilder DataBuilder { get; } = new DataBuilder();

	private ServiceProvider ServiceProvider { get; }

	private IServiceScope ServiceScope { get; }

	public IServiceProvider ScopedServiceProvider { get; }

	protected virtual IServiceCollection OverrideMocks(IServiceCollection services)
	{
		return services;
	}

	public virtual Task DisposeAsync()
	{
		ServiceScope.Dispose();
		return Task.CompletedTask;
	}

	public virtual Task InitializeAsync()
	{
		return Task.CompletedTask;
	}
}