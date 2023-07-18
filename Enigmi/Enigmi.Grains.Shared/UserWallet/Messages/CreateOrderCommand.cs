using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.OrderAggregate;

namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record CreateOrderCommand(String PaymentAddress, Guid PuzzleCollectionId, int PuzzleSize, int Quantity);

public record CreateOrderResponse(Guid OrderId, string UnsignedTransactionCborHex, uint Fee, string? Warning) : IResponse;

public record GetActiveOrderResponse(Guid? OrderId, OrderState? OrderState, uint? NumberOfConfirmations);

