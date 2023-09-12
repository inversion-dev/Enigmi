using Enigmi.Common.Messaging;

namespace Enigmi.Messages.Trade;

public record SignTradeByCounterpartCommand(Guid TradeId, string WitnessCborHex) : Command<SignTradeByCounterpartResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record SignTradeByCounterpartResponse : CommandResponse;