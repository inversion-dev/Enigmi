using Enigmi.Domain.ValueObjects;

namespace Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;

public record UserWalletTradeDetailList(string StakingAddress, List<TradeDetail> TradeDetails);