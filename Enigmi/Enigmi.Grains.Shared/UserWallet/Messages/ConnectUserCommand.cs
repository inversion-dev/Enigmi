using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record ConnectUserCommand(IEnumerable<UtxoAsset> UtxoAssets);
public record ConnectUserResponse : IResponse;