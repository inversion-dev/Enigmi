using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record ConnectUserCommand(IEnumerable<UtxoAsset> UtxoAssets, string Nickname, string PaymentAddress);

public record ConnectUserResponse : IResponse;