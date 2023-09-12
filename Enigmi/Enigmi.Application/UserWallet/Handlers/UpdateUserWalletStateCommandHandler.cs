using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;
using GrainCommand = Enigmi.Grains.Shared.UserWallet.Messages;

namespace Enigmi.Application.UserWallet.Handlers;

public class UpdateUserWalletStateCommandHandler : Handler<UpdateUserWalletStateCommand, SendWalletUtxosResponse>
{
    private IClusterClient ClusterClient { get; }

    public UpdateUserWalletStateCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<SendWalletUtxosResponse>> Execute(UpdateUserWalletStateCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakingAddress);
        await userWalletGrain.UpdateWalletState(new GrainCommand.UpdateUserWalletStateCommand(request.UtxoAssets, request.PaymentAddress));
        return new SendWalletUtxosResponse().ToSuccessResponse();
    }
}

public class UpdateUserWalletStateCommandValidator : AbstractValidator<UpdateUserWalletStateCommand>
{
    public UpdateUserWalletStateCommandValidator()
    {
        RuleFor(x => x.UtxoAssets).NotNull();
        RuleFor(x => x.PaymentAddress).NotEmpty();
        RuleFor(x => x.StakingAddress).NotEmpty();
    }
}