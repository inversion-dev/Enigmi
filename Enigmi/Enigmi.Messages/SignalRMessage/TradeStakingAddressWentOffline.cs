using Enigmi.Common.Messaging;

namespace Enigmi.Messages.SignalRMessage;

public record TradeStakingAddressWentOffline(string StakingAddress) : ISignalRMessage;