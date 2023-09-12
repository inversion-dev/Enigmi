using Enigmi.Common.Messaging;
using Enigmi.Domain.ValueObjects;
using Enigmi.Messages.ActivePuzzlePieceList;

namespace Enigmi.Messages.Trade;

public record GetTradeRequest(Guid TradeId) : Request<GetTradeResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetTradeResponse(Trade Trade) : RequestResponse;

public record Trade(Guid Id, GetPotentialTradesResponse.TradeDetail TradeDetail, TradeState TradeState, DateTime? InitiatingPartySignUtcDeadline, 
    string? TransactionId, DateTime ServerUtcTime, string? UnsignedTransactionCborHex, uint NumberOfConfirmations);