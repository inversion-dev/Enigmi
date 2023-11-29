using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.ActivePuzzlePieceListAggregate.ValueObjects;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.ActivePuzzlePieceList;
using Enigmi.Messages.UserWallet;
using FluentValidation;

namespace Enigmi.Application.UserWallet.Handlers;

public class GetActiveTradeListRequestHandler : Handler<GetActiveTradeListRequest, GetActiveTradeListResponse>
{
    private IClusterClient ClusterClient { get; }

    public GetActiveTradeListRequestHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient;
    }

    public override async Task<ResultOrError<GetActiveTradeListResponse>> Execute(GetActiveTradeListRequest request,
        CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain =
            ClusterClient.GetGrain<IUserWalletGrain>(request.StakingAddress.ThrowIfNullOrWhitespace());
        
        var trades =
            await userWalletGrain.GetActiveTradeList(
                new Enigmi.Grains.Shared.UserWallet.Messages.GetActiveTradeListRequest());
        

        return trades.Transform(x =>
            new GetActiveTradeListResponse(Convert(x.OffersMade), Convert(x.OffersReceived)));
    }

    private List<GetActiveTradeListResponse.Trade> Convert(
        IEnumerable<Domain.Entities.UserWalletActiveTradeListAggregate.Trade> trades)
    {
        return trades.Select(x =>
            new GetActiveTradeListResponse.Trade(
                x.Id,
                x.TradeState,
                x.InitiatingPartySignUtcDeadline,
                DateTime.UtcNow, 
                new GetPotentialTradesResponse.TradeDetail(GetPotentialTradesResponse.TradeParty.ConvertToParty(x.TradeDetail.InitiatingPiece),
                    GetPotentialTradesResponse.TradeParty.ConvertToParty(x.TradeDetail.CounterpartyPuzzlePiece), 
                    x.TradeDetail.Rating
                    ),
                x.TradeTimeoutInSeconds
            )).ToList();
    }
}

public class GetActiveTradeListRequestValidator : AbstractValidator<GetActiveTradeListRequest>
{
    public GetActiveTradeListRequestValidator()
    {
        RuleFor(x => x.StakingAddress).NotEmpty();
    }
}