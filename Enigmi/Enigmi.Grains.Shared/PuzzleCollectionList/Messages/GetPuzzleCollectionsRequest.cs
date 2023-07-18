namespace Enigmi.Grains.Shared.PuzzleCollectionList.Messages;

public record GetPuzzleCollectionsRequest();

public record GetPuzzleCollectionsResponse(IEnumerable<GetPuzzleCollectionsResponse.PuzzleCollectionDto> PuzzleCollections)
{
    public record PuzzleCollectionDto(
        Guid Id, 
        string Title, 
        decimal PuzzlePiecePriceInAda, 
        IEnumerable<int> PermittedPuzzleSize
    );
}


    


