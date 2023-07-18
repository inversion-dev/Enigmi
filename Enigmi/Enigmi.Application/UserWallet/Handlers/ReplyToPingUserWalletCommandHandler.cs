using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;

namespace Enigmi.Application.UserWallet.Handlers;

public class ReplyToPingUserWalletCommandHandler : Handler<ReplyToPingUserWalletCommand, PingUserWalletResponse>
{
    private IClusterClient ClusterClient { get; }

    public ReplyToPingUserWalletCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<PingUserWalletResponse>> Execute(ReplyToPingUserWalletCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakeAddress);
        await userWalletGrain.ReplyToClientPing();
        return new PingUserWalletResponse().ToSuccessResponse();
    }
}

public class ReplyToPingUserWalletCommandValidator : AbstractValidator<ReplyToPingUserWalletCommand>
{
    public ReplyToPingUserWalletCommandValidator()
    {        
    }
}