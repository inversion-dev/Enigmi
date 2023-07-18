using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record OrderUpdated(Guid OrderId, uint NumberOfConfirmations) : ISignalRMessage;