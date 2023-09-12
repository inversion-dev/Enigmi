using Enigmi.Domain.ValueObjects;

namespace Enigmi.Domain.Entities.UserWalletActiveTradeListAggregate;

public record Trade(Guid Id, TradeDetail TradeDetail, TradeState TradeState, DateTime? InitiatingPartySignUtcDeadline);

