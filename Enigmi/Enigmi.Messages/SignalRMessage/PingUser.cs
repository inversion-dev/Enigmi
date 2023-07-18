using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record PingUser(DateTime ServerUtcDateTimeNow, DateTime LastRoundTripPingReceivedUtcTimestamp, TimeSpan IdleTimeout) : ISignalRMessage;

