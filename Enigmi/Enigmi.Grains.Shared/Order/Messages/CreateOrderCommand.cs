using Domain.ValueObjects;

namespace Enigmi.Grains.Shared.Order.Messages;

public record BuildOrderCommand(string PaymentAddress, string UserWalletId, Guid PuzzleCollectionId, int PuzzleSize, List<string> OrderedPuzzlePieceIds, List<UtxoAsset> UserWalletAssets)
{
	public List<string> OrderedPuzzlePieceIds { get; set; } = OrderedPuzzlePieceIds ?? new List<string>();
	public List<UtxoAsset> UserWalletAssets { get; set; } = UserWalletAssets?? new List<UtxoAsset>();
}

public record BuildOrderResponse(List<Utxo> UsedUtxos, string UnsignedTransactionCborHex, uint Fee);