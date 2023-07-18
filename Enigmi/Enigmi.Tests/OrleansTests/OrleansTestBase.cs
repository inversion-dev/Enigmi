using System;
using System.Threading.Tasks;
using Enigmi.Common;
using Enigmi.Grains.Shared.GrainSettings;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Enigmi.Tests.OrleansTests;

public class OrleansTestBase : IAsyncLifetime
{
    private ClusterFixture ClusterFixture { get; }

    public OrleansTestBase(ClusterFixture clusterFixture)
    {
        ClusterFixture = clusterFixture;
        Setting = HostSetup.ConfigurationExtensions.GetConfiguration().GetSection("Settings").Get<Settings>()!;
    }

    public Settings Setting { get; set; }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(this.Setting.EnvironmentPrefix))
        {
            throw new Exception(@"No environment prefix found. Prefix is required to ensure that unit test are executed in isolation for each developer.  
                                Please specify the prefix at Settings.EnvironmentPrefix in your appsettings.local.json");
        }

        var settingsGrain = ClusterFixture.ClusterClient.GetGrain<IGrainSettingsGrain>(0);
        var settings = await settingsGrain.GetSettings();
        settings.UserWalletRoundTripPingInterval = TimeSpan.FromMinutes(1);
        settings.UserWalletOnlineIdleTimeout = TimeSpan.FromMinutes(5);
        settings.OrderGrain.OrderExpiresTimespan = TimeSpan.FromMinutes(5);
        await settingsGrain.UpdateSettings(settings);

        await Task.CompletedTask;
    }
    
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    
}