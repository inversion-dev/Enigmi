using Enigmi.Common.Messaging;

namespace Enigmi.Messages.Trade;

public record BuildTransactionCommand(Guid TradeId) : Command<BuildTransactionResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record BuildTransactionResponse(string UnsignedCbor, uint Fee) : CommandResponse;    