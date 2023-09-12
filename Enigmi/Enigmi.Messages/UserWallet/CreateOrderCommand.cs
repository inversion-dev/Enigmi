using Domain.ValueObjects;
using Enigmi.Common.Messaging;

namespace Enigmi.Messages.UserWallet;

public record CreateOrderCommand(string StakeAddress, Guid CollectionId, int PuzzleSize, int Quantity)  : Command<CreateOrderResponse>, IHasWalletState
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;
    
    public IEnumerable<UtxoAsset> UtxoAssets { get; set; } = null!;

    public string PaymentAddress { get; set; }  = null!;
}

public record CreateOrderResponse(Guid OrderId, string UnsignedTransactionCborHex, uint TransactionFee, string? Warning) : CommandResponse;
