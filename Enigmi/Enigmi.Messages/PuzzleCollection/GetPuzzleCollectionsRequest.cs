using Enigmi.Common.Messaging;

namespace Enigmi.Messages.PuzzleCollection;

public record GetPuzzleCollectionsRequest : Request<GetPuzzleCollectionsResponse>
{ 
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
}

public record GetPuzzleCollectionsResponse(IEnumerable<GetPuzzleCollectionsResponse.PuzzleCollection> ResponsePuzzleCollections) : RequestResponse
{
     public record PuzzleCollection(Guid Id, string Title, decimal PuzzlePiecePriceInAda, IEnumerable<int> PermittedPuzzleSize); 
}

 