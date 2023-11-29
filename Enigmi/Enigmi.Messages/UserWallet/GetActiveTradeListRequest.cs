using Enigmi.Common.Messaging;
using Enigmi.Domain.ValueObjects;
using Enigmi.Messages.ActivePuzzlePieceList;

namespace Enigmi.Messages.UserWallet;

public record GetActiveTradeListRequest(string StakingAddress) : Request<GetActiveTradeListResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetActiveTradeListResponse(List<GetActiveTradeListResponse.Trade> OffersMade,
    List<GetActiveTradeListResponse.Trade> OffersReceived) : RequestResponse
{
    public record Trade(Guid Id, TradeState State, DateTime? InitiatingPartySignUtcDeadline, DateTime ServerUtcDateTime, GetPotentialTradesResponse.TradeDetail TradeDetails
    , int TradeTimeoutInSeconds);
}

