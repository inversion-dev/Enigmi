using Enigmi.Domain.ValueObjects;

namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record GetActiveTradeListRequest();

public record GetActiveTradeListResponse(List<TradeDetail> TradeDetails);