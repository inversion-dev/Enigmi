using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record OrderCompleted(Guid OrderId) : ISignalRMessage;