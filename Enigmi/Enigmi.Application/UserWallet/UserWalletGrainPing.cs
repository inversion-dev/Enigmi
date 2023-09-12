using Enigmi.Application.ExtensionMethods;
using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.SignalRMessage;

namespace Enigmi.Application.UserWallet;

public partial class UserWalletGrain : GrainBase<Domain.Entities.UserWalletAggregate.UserWallet>, IUserWalletGrain
{
    private IDisposable? PingTimer { get; set; }

    private TimeSpan PingTimerInterval { get; set; }

    private int PingCount { get; set; } = 0;

    private async Task<TimeSpan> GetPingInterval()
    {
        var settingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
        var settings = await settingsGrain.GetSettings();
        return settings.UserWalletRoundTripPingInterval;
    }

    private async Task PingUserHandler(object state)
    {
        await this.SelfInvokeAfter<IUserWalletGrain>(o => o.PingUser());
    }

    public async Task PingUser()
    {
        var settingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
        var settings = await settingsGrain.GetSettings();
        var lastRoundTripPingReceivedUtcTimestamp = State.DomainAggregate?.LastRoundTripPingReceivedUtcTimestamp;
        var utcNow = DateTime.UtcNow;
        if (UserWalletUtil.HasUserWalletOnlineStatusTimedOut(utcNow, lastRoundTripPingReceivedUtcTimestamp, settings.UserWalletOnlineIdleTimeout))
        {
            State.DomainAggregate?.GoOffline();
            await WriteStateAsync();
        }
        else
        {
            await SendSignalRMessage(new PingUser(utcNow, lastRoundTripPingReceivedUtcTimestamp!.Value, settings.UserWalletOnlineIdleTimeout));
            await ReschedulePingTimerIfRequired();
        }
    }

    public async Task<ResultOrError<Constants.Unit>> ReplyToClientPing()
    {
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.KeepAlive();
        PingCount++;
        if (PingCount % 3 == 0) //only write state every third call
        {
            await WriteStateAsync();
        }

        return new Constants.Unit().ToSuccessResponse();
    }

    private async Task ReschedulePingTimerIfRequired()
    {
        var pingInterval = await GetPingInterval();
        if (!PingTimerInterval.Equals(pingInterval) || PingTimer == null)
        {
            PingTimer?.Dispose();
            SchedulePingTimer(pingInterval);
        }
    }

    private void SchedulePingTimer(TimeSpan pingInterval)
    {
        PingTimer = RegisterTimer(PingUserHandler, this, pingInterval, pingInterval);
        PingTimerInterval = pingInterval;
    }
}