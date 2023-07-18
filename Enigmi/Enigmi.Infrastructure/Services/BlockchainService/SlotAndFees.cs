namespace Enigmi.Infrastructure.Services.BlockchainService;

public record CardanoSlotAndFees(CardanoSlot Slot, CardanoNetworkFee CardanoNetworkFee);
public record CardanoSlot(uint Slot, DateTime SlotUtcTimestamp);
public record CardanoNetworkFee(uint MinFeeA, uint MinFeeB);