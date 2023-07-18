namespace Enigmi.Common;

public static class UserWalletUtil
{
    public static bool HasUserWalletOnlineStatusTimedOut(DateTime utcNow, DateTime? lastRoundTripPingReceivedUtcTimestamp, TimeSpan idleTimeout)
    {
        return lastRoundTripPingReceivedUtcTimestamp == null || (utcNow - lastRoundTripPingReceivedUtcTimestamp.Value).CompareTo(idleTimeout) >= 0;
    }
}