namespace Enigmi.Grains.Shared.Trade.Messages;

public record CreateTransactionCommand;
public record CreateTransactionResponse(string UnsignedCbor, uint Fee);