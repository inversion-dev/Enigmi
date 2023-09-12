namespace Enigmi.Grains.Shared.UserWalletActiveTradeList;

public interface IUserWalletActiveTradeListGrain : IGrainWithStringKey
{
    Task<IEnumerable<Domain.Entities.UserWalletActiveTradeListAggregate.Trade>> GetActiveTrades();
    
    Task Create();
}
