using Domain.ValueObjects;
using Enigmi.Domain.ValueObjects;

namespace Enigmi.Grains.Shared.Trade.Messages;

public record CreateTradeCommand(TradeDetail TradeDetail, IEnumerable<Utxo> InitiatingWalletReservedUtxoAssets, IEnumerable<Utxo> CounterpartyWalletReservedUtxoAssets,
    List<Utxo> InitiatingPuzzlePieceUtxos,
    List<Utxo> CounterpartyPuzzlePieceUtxos);

public record CreateTradeResponse;