using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;

namespace Enigmi.Application.UserWallet.Handlers;

public class MakeAnOfferCommandHandler : Handler<MakeAnOfferCommand, MakeAnOfferResponse>
{
    private IClusterClient ClusterClient { get; }

    public MakeAnOfferCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<MakeAnOfferResponse>> Execute(MakeAnOfferCommand request,
        CancellationToken cancellationToken)
    {
        request.ThrowIfNull();

        var userWalletGrain =
            ClusterClient.GetGrain<IUserWalletGrain>(request.StakingAddress.ThrowIfNullOrWhitespace());

        var makeAnOfferResponse = await userWalletGrain.MakeAnOffer(
            new Enigmi.Grains.Shared.UserWallet.Messages.MakeAnOfferCommand(request.Offers.Select(x =>
                new Enigmi.Grains.Shared.UserWallet.Messages.MakeAnOfferCommand.Offer(x.InitiatingPuzzlePieceId, x.CounterpartyPuzzlePieceId, x.CounterpartyStakingAddress, x.CounterpartyNickname)
            ).ToList()
            ));
        

        return makeAnOfferResponse.Transform(x => new MakeAnOfferResponse(x.SuccessfulOfferCount, x.OfferCount, x.Errors));
    }
}

public class MakeAnOfferCommandValidator : AbstractValidator<MakeAnOfferCommand>
{
    public MakeAnOfferCommandValidator()
    {
        RuleFor(x => x.StakingAddress).NotEmpty();
        RuleFor(x => x.Offers).NotEmpty();
    }
}