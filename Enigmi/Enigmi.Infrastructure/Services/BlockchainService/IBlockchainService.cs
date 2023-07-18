namespace Enigmi.Infrastructure.Services.BlockchainService;

public interface IBlockchainService
{
    Task<CardanoSlot> GetLatestSlotAndUtcTimestampAsync();
    
    Task<CardanoSlotAndFees> GetSlotAndFeesAsync();
    
    Task<string?> SubmitTransactionAsync(string transactionCbor);
    
    Task<Transaction> GetTransactionAsync(string txId);
    
    Task<uint?> GetConfirmationsAsync(uint txBlockHeight);
}