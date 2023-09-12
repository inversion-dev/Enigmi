namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record MakeAnOfferCommand(List<MakeAnOfferCommand.Offer> Offers)
{
    public record Offer(string InitiatingPuzzlePieceId, string CounterpartyPuzzlePieceId, string CounterpartyStakingAddress, string CounterpartyNickname);
}

public record MakeAnOfferResponse(int OfferCount, int SuccessfulOfferCount, List<(string nickname,string error)> Errors);