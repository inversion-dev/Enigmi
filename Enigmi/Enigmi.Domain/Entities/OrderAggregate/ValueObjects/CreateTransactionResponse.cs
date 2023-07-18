using Enigmi.Common.Messaging;

namespace Enigmi.Domain.Entities.OrderAggregate.ValueObjects;

public record CreateTransactionResponse(string UnsignedTransactionCborHex, uint Fee) : IResponse;