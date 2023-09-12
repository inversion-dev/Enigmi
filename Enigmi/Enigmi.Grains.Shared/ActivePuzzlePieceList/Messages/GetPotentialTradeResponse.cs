using Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;

namespace Enigmi.Grains.Shared.ActivePuzzlePieceList.Messages;

public record GetPotentialTradeResponse(List<UserWalletTradeDetailList> UserWalletTradeDetails);





