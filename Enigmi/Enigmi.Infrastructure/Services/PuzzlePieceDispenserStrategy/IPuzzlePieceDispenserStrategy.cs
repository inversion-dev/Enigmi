namespace Enigmi.Infrastructure.Services.PuzzlePieceDispenserStrategy;

public interface IPuzzlePieceDispenserStrategy
{
    List<string> GetPuzzlePieceIds(IEnumerable<string> puzzlePieceIds, int count);
}