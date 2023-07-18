using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;

namespace Enigmi.Grains.Shared.PuzzleCollectionSniffer;

public interface IPuzzleCollectionSnifferGrain : IGrainWithStringKey
{
    Task<ResultOrError<SeedPuzzleCollectionResponse>> SeedPuzzleCollection(SeedPuzzleCollectionCommand puzzleCollectionDto);
}