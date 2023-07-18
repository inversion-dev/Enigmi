using Domain.ValueObjects;

namespace Enigmi.Messages;

public interface IHasWalletState
{
    IEnumerable<UtxoAsset> UtxoAssets { get; set; }
}