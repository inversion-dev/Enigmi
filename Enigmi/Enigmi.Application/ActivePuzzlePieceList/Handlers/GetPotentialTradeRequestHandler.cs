using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.TradeAggregate;
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
        var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);

        var response = await activePuzzlePieceListGrain.FindPotentialTrades(request.StakingAddress, request.PuzzlePieceDefinitionId);

        return new GetPotentialTradesResponse(response.UserWalletTradeDetails.Select(y => 
                    new GetPotentialTradesResponse.UserWalletTradeDetailList(y.StakingAddress, 
                    y.TradeDetails.Select(x =>
                        new GetPotentialTradesResponse.TradeDetail
                        (
                            new GetPotentialTradesResponse.TradeParty(
                                x.InitiatingPiece.PuzzlePieceId,
                                x.InitiatingPiece.PuzzlePieceDefinitionId,
                                x.InitiatingPiece.PuzzleDefinitionId, 
                                x.InitiatingPiece.PuzzleDefinitionTitle,
                                x.InitiatingPiece.PuzzleCollectionId,
                                x.InitiatingPiece.PuzzleCollectionTitle,
                                x.InitiatingPiece.StakingAddress,
                                x.InitiatingPiece.Nickname,
                                x.InitiatingPiece.Rating,
                                new GetPotentialTradesResponse.PuzzleDefinitionInventory
                                {
                                    PuzzlePieceDefinitionIds  = x.InitiatingPiece.OutgoingPuzzleDefinitionPieceDefinitionInventory.Select(z => 
                                        new GetPotentialTradesResponse.PuzzleDefinitionInventoryItem(z.PuzzlePieceDefinitionId, z.Quantity)).ToList()
                                },
                                new GetPotentialTradesResponse.PuzzleDefinitionInventory
                                {
                                    PuzzlePieceDefinitionIds  = x.InitiatingPiece.IncomingPuzzleDefinitionPieceDefinitionInventory.Select(z => 
                                        new GetPotentialTradesResponse.PuzzleDefinitionInventoryItem(z.PuzzlePieceDefinitionId, z.Quantity)).ToList()
                                }
                            ),
                            new GetPotentialTradesResponse.TradeParty(
                                x.CounterpartyPuzzlePiece.PuzzlePieceId,
                                x.CounterpartyPuzzlePiece.PuzzlePieceDefinitionId,
                                x.CounterpartyPuzzlePiece.PuzzleDefinitionId,
                                x.CounterpartyPuzzlePiece.PuzzleDefinitionTitle,
                                x.CounterpartyPuzzlePiece.PuzzleCollectionId,
                                x.CounterpartyPuzzlePiece.PuzzleCollectionTitle,
                                x.CounterpartyPuzzlePiece.StakingAddress,
                                x.CounterpartyPuzzlePiece.Nickname,
                                x.CounterpartyPuzzlePiece.Rating,
                                new GetPotentialTradesResponse.PuzzleDefinitionInventory
                                {
                                    PuzzlePieceDefinitionIds  = x.CounterpartyPuzzlePiece.OutgoingPuzzleDefinitionPieceDefinitionInventory.Select(z => 
                                        new GetPotentialTradesResponse.PuzzleDefinitionInventoryItem(z.PuzzlePieceDefinitionId, z.Quantity)).ToList()
                                },
                                new GetPotentialTradesResponse.PuzzleDefinitionInventory
                                {
                                    PuzzlePieceDefinitionIds  = x.CounterpartyPuzzlePiece.IncomingPuzzleDefinitionPieceDefinitionInventory.Select(z => 
                                        new GetPotentialTradesResponse.PuzzleDefinitionInventoryItem(z.PuzzlePieceDefinitionId, z.Quantity)).ToList()
                                }
                            ),
                            x.Rating
                        )
                ).ToList()
            )).ToList())
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