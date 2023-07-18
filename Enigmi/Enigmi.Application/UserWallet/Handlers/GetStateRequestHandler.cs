using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;

namespace Enigmi.Application.UserWallet.Handlers;

public class GetStateRequestHandler : Handler<GetStateRequest, GetStateResponse>
{
    private IClusterClient ClusterClient { get; }

    public GetStateRequestHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<GetStateResponse>> Execute(GetStateRequest request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakeAddress);
        Enigmi.Grains.Shared.UserWallet.Messages.GetStateResponse? state;

        if (request.OrderId.HasValue)
        {
            state = await userWalletGrain.GetActiveCompletedOrderPuzzlePieces(request.OrderId.Value);
        }
        else
        {
            state = await userWalletGrain.GetState();
        }

        state.ThrowIfNull();

        return new GetStateResponse(
            state.PuzzlePieces
                .Select(x => new GetStateResponse.PuzzlePiece(
                    x.PuzzlePieceDefinitionId, x.PuzzleDefinitionId, x.ImageUrl, x.X, x.Y, x.IsOwned, 
                    x.OwnedPuzzlePieceIds,
                    x.PuzzlePieceCount))
                .ToList(),
            state.PuzzleDefinitions.Select
            (x => new GetStateResponse.PuzzleDefinition
                (
                    x.Id,
                    x.Title,
                    x.PuzzleSize,
                    x.NumberOfAllowedBuilds,
                    x.NumberOfCompletedBuilds
                )
            ).ToList()
        ).ToSuccessResponse();
    }
}

public class GetStateRequestValidator : AbstractValidator<GetStateRequest>
{
    public GetStateRequestValidator()
    {
        RuleFor(x => x.StakeAddress).NotEmpty();
    }
}