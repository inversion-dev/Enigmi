using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.ActivePuzzlePieceList;
using Enigmi.Messages.ActivePuzzlePieceList;
using FluentValidation;

namespace Enigmi.Application.ActivePuzzlePieceList.Handlers;

public class GetPuzzleDefinitionsRequestHandler : Handler<GetPuzzleDefinitionsRequest, GetPuzzleDefinitionsResponse>
{
    private IClusterClient ClusterClient { get; }

    public GetPuzzleDefinitionsRequestHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient;
    }
    public override async Task<ResultOrError<GetPuzzleDefinitionsResponse>> Execute(GetPuzzleDefinitionsRequest request, CancellationToken cancellationToken)
    {
        var activePuzzlePieceListGrain = ClusterClient.GetGrain<IActivePuzzlePieceListGrain>(Constants.SingletonGrain);
        var response = await activePuzzlePieceListGrain.GetPuzzleDefinitions(request.PuzzlePieceDefinitionIds);
        
        return response.Transform(x => new GetPuzzleDefinitionsResponse(
            x.PuzzleDefinitions.Select(y => new GetPuzzleDefinitionsResponse.PuzzleDefinition(
                    y.Id,
                    y.Title,
                    y.CollectionTitle,
                    y.PuzzleSize,
                    y.NumberOfAllowedBuilds,
                    y.NumberOfCompletedBuilds,
                    y.PuzzlePieceDefinitions.Select(z => new GetPuzzleDefinitionsResponse.PuzzlePieceDefinition(
                        z.Id,
                        z.PuzzleDefinitionId,
                        z.ImageUrl
                    )).ToList().AsEnumerable()
            )).ToList()));
    }
}

public class GetPuzzleDefinitionsRequestValidator : AbstractValidator<GetPuzzleDefinitionsRequest>
{
    public GetPuzzleDefinitionsRequestValidator()
    {
        RuleFor(x => x.PuzzlePieceDefinitionIds).NotEmpty();
    }
}