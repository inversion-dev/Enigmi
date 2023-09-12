using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record TradeUpdated(Guid TradeId) : ISignalRMessage;