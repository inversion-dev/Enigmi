namespace Enigmi.Infrastructure.Services.PuzzlePieceDispenserStrategy;

public class RandomPuzzlePieceDispenserStrategy : IPuzzlePieceDispenserStrategy 
{
    public List<string> GetPuzzlePieceIds(IEnumerable<string> puzzlePieceIds, int count)
    {
        return puzzlePieceIds.OrderBy(o => Guid.NewGuid())
            .Take(count)
            .ToList();
    }
}