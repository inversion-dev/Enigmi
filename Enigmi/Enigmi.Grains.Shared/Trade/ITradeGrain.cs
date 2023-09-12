using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.Trade.Messages;

namespace Enigmi.Grains.Shared.Trade;

public interface ITradeGrain : IGrainWithGuidKey
{
    Task<ResultOrError<CreateTradeResponse>> CreateTrade(CreateTradeCommand command);

    Task<Domain.Entities.TradeAggregate.Trade> GetTrade();

    Task<ResultOrError<Constants.Unit>> GoOffline(string stakingAddress);
    
    Task<ResultOrError<CreateTransactionResponse>> BuildTransaction(CreateTransactionCommand command);
    
    Task<ResultOrError<Constants.Unit>> SignByCounterparty(SignTradeByCounterpartyCommand command);
    
    Task<ResultOrError<Constants.Unit>> SignTradeByInitiatingParty(SignTradeByInitiatingPartyCommand command);
}