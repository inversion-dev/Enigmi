namespace Enigmi.Infrastructure.Services.PuzzlePieceDispenserStrategy;

public class SequentialPuzzlePieceDispenserStrategy : IPuzzlePieceDispenserStrategy 
{
    public List<string> GetPuzzlePieceIds(IEnumerable<string> puzzlePieceIds, int count)
    {
        return puzzlePieceIds
            .Take(count)
            .ToList();
    }
}