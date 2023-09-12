namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record GetTradeResponse(IEnumerable<Domain.Entities.UserWalletActiveTradeListAggregate.Trade> OffersMade, 
    IEnumerable<Domain.Entities.UserWalletActiveTradeListAggregate.Trade> OffersReceived);