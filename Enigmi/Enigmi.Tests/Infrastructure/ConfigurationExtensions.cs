using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Foundatio.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Enigmi.Tests;

public static class ConfigurationExtensions
{
	public static IServiceCollection ConfigureTestServices(this IServiceCollection services)
	{
		var configuration = HostSetup.ConfigurationExtensions.GetConfiguration();

		Settings settings = services.ConfigureSettings(configuration);

		services.AddSingleton<ICacheClient, InMemoryCacheClient>();

		services.AddScoped(o => new ScopedInformation().WithUserContext(new AuthorizedUserContext("Test")));

		return services;
	}

	private static Settings ConfigureSettings(this IServiceCollection services, IConfiguration configuration)
	{
		var settings = new Settings();
		configuration.GetSection("Settings").Bind(settings);
		services.AddTransient(o => settings);
		return settings;
	}
}