using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.OrderAggregate;

public class TradeInPuzzlePiece
{
	public TradeInPuzzlePiece(Guid id, string blockchainAssetId, decimal tradeInValue)
	{
		Id = id;
		BlockchainAssetId = blockchainAssetId;
		TradeInValue = tradeInValue;
	}

    [JsonProperty]
    public Guid Id { get; private set; }

    [JsonProperty]
    public string BlockchainAssetId { get; private set; }

    [JsonProperty]
    public decimal TradeInValue { get; }
}