using Enigmi.Common;

namespace Enigmi.Infrastructure.Services.BlockchainService;

public class Transaction
{
    public string TxId { get; set; }

    public string? BlockHash { get; set; }

    public uint? BlockHeight { get; set; }

    public DateTime? BlockUtcTimestamp { get; set; }

    public uint? Slot { get; set; }

    public Transaction(string txId)
    {
        TxId = txId.ThrowIfNull();
    }
}