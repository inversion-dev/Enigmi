using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record TradeAvailabilityChanged(Guid TradeId, bool IsAvailable) : ISignalRMessage;