using Enigmi.Common;
using Enigmi.Common.Domain;
using Newtonsoft.Json;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;
using static System.FormattableString;

namespace Enigmi.Domain.Entities.UserWalletActiveTradeListAggregate;

public class UserWalletActiveTradeList : DomainEntity
{
    public UserWalletActiveTradeList(string id)
    {
        Id = id.ThrowIfNullOrWhitespace();
    }

    [JsonProperty]
    public string Id { get; set; }

    [JsonProperty]
    private List<Trade> _activeTrades = new();

    public IEnumerable<Trade> ActiveTrades => _activeTrades.AsReadOnly();

    public void AddTrade(Trade tradeDetail)
    {
        tradeDetail.ThrowIfNull();
        _activeTrades.Add(tradeDetail);
    }

    public void RemoveTrade(Guid tradeId)
    {
        tradeId.ThrowIfEmpty();
        _activeTrades.RemoveAll(x => x.Id == tradeId);
    }

    public void UpdateTrade(Trade trade)
    {
        trade.ThrowIfNull();
        _activeTrades.RemoveAll(x => x.Id == trade.Id);
        _activeTrades.Add(trade);
    }
}