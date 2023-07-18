using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using FluentValidation;
using Enigmi.Messages.ActivePuzzlePieceList;

namespace Enigmi.Application.ActivePuzzlePieceList.Handlers;

public class GetPotentialTradeRequestHandler : Handler<GetPotentialTradesRequest, GetPotentialTradesResponse>
{
    private IClusterClient ClusterClient { get; }

    public GetPotentialTradeRequestHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<GetPotentialTradesResponse>> Execute(GetPotentialTradesRequest request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(0);

        var response = await activePuzzlePieceListGrain.FindPotentialTrades(request.StakingAddress, request.PuzzlePieceDefinitionId);

        return new GetPotentialTradesResponse(response.TradeDetails
            .Select(x => new GetPotentialTradesResponse.TradeDetail(
                new GetPotentialTradesResponse.TradePuzzlePiece(
                    x.OwnedPuzzlePiece.PuzzlePieceId, 
                    x.OwnedPuzzlePiece.PuzzleDefinitionId, 
                    x.OwnedPuzzlePiece.PuzzleDefinitionTitle,
                    x.OwnedPuzzlePiece.PuzzleCollectionId,
                    x.OwnedPuzzlePiece.PuzzleCollectionTitle,
                    x.OwnedPuzzlePiece.StakingAddress,
                    x.OwnedPuzzlePiece.Rating
                    ),
                new GetPotentialTradesResponse.TradePuzzlePiece(
                    x.TradePuzzlePiece.PuzzlePieceId,
                    x.TradePuzzlePiece.PuzzleDefinitionId,
                    x.TradePuzzlePiece.PuzzleDefinitionTitle,
                    x.TradePuzzlePiece.PuzzleCollectionId,
                    x.TradePuzzlePiece.PuzzleCollectionTitle,
                    x.TradePuzzlePiece.StakingAddress,
                    x.TradePuzzlePiece.Rating
                    ),
                x.Rating
                )
            ).ToList())
            .ToSuccessResponse();
    }
}

public class GetPotentialTradeRequestValidator : AbstractValidator<GetPotentialTradesRequest>
{
    public GetPotentialTradeRequestValidator()
    {
        RuleFor(x => x.StakingAddress).NotEmpty();
        RuleFor(x => x.PuzzlePieceDefinitionId).NotEmpty();
    }
}