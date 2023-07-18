using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record UpdateUserWalletStateCommand(IEnumerable<UtxoAsset> Utxos);

public record UpdateUserWalletStateResponse : IResponse;