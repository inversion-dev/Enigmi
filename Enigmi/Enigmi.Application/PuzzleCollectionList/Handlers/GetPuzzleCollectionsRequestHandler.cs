using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PuzzleCollectionList;
using Enigmi.Messages.PuzzleCollection;
using FluentValidation;

namespace Enigmi.Application.PuzzleCollectionList.Handlers;

public class GetPuzzleCollectionsRequestHandler : Handler<GetPuzzleCollectionsRequest, GetPuzzleCollectionsResponse>
{
    private IClusterClient ClusterClient { get; }

    public GetPuzzleCollectionsRequestHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<GetPuzzleCollectionsResponse>> Execute(GetPuzzleCollectionsRequest request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var puzzleCollectionSnifferGrain = ClusterClient.GetGrain<IPuzzleCollectionListGrain>(Constants.SingletonGrain);
        var response = await puzzleCollectionSnifferGrain.GetPuzzleCollections(new Enigmi.Grains.Shared.PuzzleCollectionList.Messages.GetPuzzleCollectionsRequest());

        return response.Transform(o => new GetPuzzleCollectionsResponse(o.PuzzleCollections.Select(x =>
            new GetPuzzleCollectionsResponse.PuzzleCollection(
                x.Id,
                x.Title,
                x.PuzzlePiecePriceInAda,
                x.PermittedPuzzleSize
            ))));
    }
}

public class GetPuzzleCollectionsRequestValidator : AbstractValidator<GetPuzzleCollectionsRequest>
{
    public GetPuzzleCollectionsRequestValidator()
    {
        
    }
}