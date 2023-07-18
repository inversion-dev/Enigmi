using Enigmi.Common;
using Newtonsoft.Json;

namespace Enigmi.Domain.Entities.OrderAggregate;

public class OrderedPuzzlePiece
{
	public OrderedPuzzlePiece(string id, string blockchainAssetId, string policyId, ulong priceInLovelace)
	{
		Id = id.ThrowIfNullOrWhitespace();
		BlockchainAssetId = blockchainAssetId.ThrowIfNullOrWhitespace();
		PolicyId = policyId;
		PriceInLovelace = priceInLovelace;
	}

    [JsonProperty]
    public string Id { get; private set; }

    [JsonProperty]
    public string BlockchainAssetId { get; private set; }

    [JsonProperty]
    public string PolicyId { get; private set; }

    [JsonProperty]
    public ulong PriceInLovelace { get; }
}