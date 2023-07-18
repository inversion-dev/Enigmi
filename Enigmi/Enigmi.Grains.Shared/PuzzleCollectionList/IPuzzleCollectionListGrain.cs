using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PuzzleCollectionList.Messages;

namespace Enigmi.Grains.Shared.PuzzleCollectionList;

public interface IPuzzleCollectionListGrain : IGrainWithIntegerKey
{
    Task<ResultOrError<GetPuzzleCollectionsResponse>> GetPuzzleCollections(GetPuzzleCollectionsRequest request);
}