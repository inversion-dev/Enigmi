using System;
using System.Threading.Tasks;
using Enigmi.Grains.Shared.GrainSettings;
using FluentAssertions;
using Orleans;
using Orleans.TestingHost;
using Xunit;

namespace Enigmi.Tests.OrleansTests;

[Collection(ClusterCollection.Name)]
public class SettingsTests : OrleansTestBase
{
    private IClusterClient ClusterClient { get; }

    public SettingsTests(ClusterFixture fixture) : base(fixture)
    {
        ClusterClient = fixture.ClusterClient;
    }
    
    [Fact]
    public async Task ShouldSucceedWhenSettingWalletRoundTripPingInterval()
    {
        var settingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
        var settings = await settingsGrain.GetSettings();
        settings.UserWalletRoundTripPingInterval = TimeSpan.FromMinutes(2);
        await settingsGrain.UpdateSettings(settings);
        var settings2 = await settingsGrain.GetSettings();
        settings2.UserWalletRoundTripPingInterval.Should().Be(TimeSpan.FromMinutes(2));
    }
    
    [Fact]
    public async Task ShouldFailWhenSettingVersionHasMismatch()
    {
        var settingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(0);
        var settings = await settingsGrain.GetSettings();
        await settingsGrain.UpdateSettings(settings); //simulate settings updated by somebody else
        var response = await settingsGrain.UpdateSettings(settings);
        response.HasErrors.Should().BeTrue();
    }
}