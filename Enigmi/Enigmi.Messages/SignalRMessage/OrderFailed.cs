using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record OrderFailed(Guid OrderId) : ISignalRMessage;