using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Messages.ActivePuzzlePieceList;
using Enigmi.Messages.Trade;
using GetTradeResponse = Enigmi.Messages.Trade.GetTradeResponse;

namespace Enigmi.Application.Trade.Handlers;

public class GetTradeRequestHandler : Handler<GetTradeRequest, GetTradeResponse>
{
    private IClusterClient ClusterClient { get; }

    public GetTradeRequestHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient;
    }

    public override async Task<ResultOrError<GetTradeResponse>> Execute(GetTradeRequest request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(request.TradeId);
        var trade = await tradeGrain.GetTrade();

        var response = new GetTradeResponse(new Messages.Trade.Trade(
            trade.Id,
            new GetPotentialTradesResponse.TradeDetail(
                GetPotentialTradesResponse.TradeParty.ConvertToParty(trade.TradeDetail.InitiatingPiece),
                GetPotentialTradesResponse.TradeParty.ConvertToParty(trade.TradeDetail.CounterpartyPuzzlePiece),
                trade.TradeDetail.Rating),
            trade.State,
            trade.InitiatingPartySignUtcDeadline,
            trade.BlockchainTransaction?.TransactionId,
            DateTime.UtcNow,
            trade.BlockchainTransaction?.UnsignedTransactionCborHex,
            trade.NumberOfConfirmations));

        return response.ToSuccessResponse();
    }
}