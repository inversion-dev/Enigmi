using Enigmi.Common.Messaging;
using static Enigmi.Messages.UserWallet.MakeAnOfferCommand;

namespace Enigmi.Messages.UserWallet;

public record MakeAnOfferCommand(string StakingAddress, List<Offer> Offers) : Command<MakeAnOfferResponse>
{
    public override Enums.AccessMechanism AccessMechanism { get; } = Enums.AccessMechanism.Authorized;

    public record Offer(string InitiatingStakingAddress, string InitiatingPuzzlePieceId, string CounterpartyPuzzlePieceId, string CounterpartyStakingAddress, string CounterpartyNickname);
}

public record MakeAnOfferResponse(int SuccessfulOfferCount, int OfferCount, List<(string nickname,string error)> Errors) : CommandResponse;
