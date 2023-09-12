using Enigmi.Common.Messaging;

namespace Enigmi.Messages.Trade;

public record SignTradeByInitiatingPartyCommand(Guid TradeId, string WitnessCborHex) : Command<SignTradeByInitiatingPartyResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}   
public record SignTradeByInitiatingPartyResponse : CommandResponse;