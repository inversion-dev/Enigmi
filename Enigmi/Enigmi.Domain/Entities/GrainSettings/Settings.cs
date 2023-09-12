namespace Enigmi.Domain.Entities.GrainSettings;

public class Settings
{
    public TimeSpan UserWalletOnlineIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan UserWalletRoundTripPingInterval { get; set; } = TimeSpan.FromMinutes(1);

    public OrderBlockchainTransactionSettingsConfig OrderBlockchainTransactionSettings = new();
    
    public OrderGrainSettings OrderGrain { get; set; } = new OrderGrainSettings();
    
    public PuzzlePieceDispenserGrainSettings PuzzlePieceDispenserGrain { get; set; } = new PuzzlePieceDispenserGrainSettings();
    
    public ActivePuzzlePieceListSettings ActivePuzzlePieceList { get; set; } = new ActivePuzzlePieceListSettings();

    public TradeGrainSettings TradeGrain { get; set; } = new TradeGrainSettings();

    public int Version { get; set; }

    public sealed class OrderBlockchainTransactionSettingsConfig
    {
        public int TtlBufferInMinutes { get; set; } = 5;

        public int ConfirmationThreshold { get; set; } = 10;

        public int MaxTransientRejectedCount { get; set; } = 5;

        public int Ttl { get; set; } = 100 * 60; //100 minutes
    }

    public sealed class OrderGrainSettings
    {
        public TimeSpan OrderExpiresTimespan = TimeSpan.FromMinutes(5);
    }
    
    public sealed class PuzzlePieceDispenserGrainSettings
    {
        public TimeSpan DispenserExpiresTimespan = TimeSpan.FromMinutes(6);
    }

    public sealed class TradeGrainSettings
    {
        public TimeSpan InitiatingPartySignTimespanDeadline = TimeSpan.FromMinutes(2);
    }    

    public sealed class ActivePuzzlePieceListSettings
    {
        public int MaxStakingAddressAddressReturnCount { get; set; } = 3;

        public int MaxTradeDetailsReturnCount { get; set; } = 5;
    }
}
