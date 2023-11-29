namespace Enigmi.Grains.Shared.ActiveUtxoReservationsList;

public interface IActiveUtxoReservationsListGrain : IGrainWithStringKey
{
    Task Initialize();
    
    Task<IEnumerable<string>> GetReservedPuzzlePieces();
}