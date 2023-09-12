using Enigmi.Domain.ValueObjects;

namespace Enigmi.Grains.Shared.Trade.Messages;

public record CreateTradeCommand(TradeDetail TradeDetail);

public record CreateTradeResponse;