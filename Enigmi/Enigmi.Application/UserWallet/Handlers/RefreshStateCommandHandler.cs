using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;
using UpdateUserWalletStateCommand = Enigmi.Grains.Shared.UserWallet.Messages.UpdateUserWalletStateCommand;

namespace Enigmi.Application.UserWallet.Handlers;

public class RefreshStateCommandHandler : Handler<RefreshStateCommand, RefreshStateResponse>
{
    private IClusterClient ClusterClient { get; }

    public RefreshStateCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }
    
    public override async Task<ResultOrError<RefreshStateResponse>> Execute(RefreshStateCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakeAddress);

        var response = await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(request.UtxoAssets));
        return response.Transform(o => new RefreshStateResponse());
    }
}

public class RefreshStateCommandValidator : AbstractValidator<RefreshStateCommand>
{
    public RefreshStateCommandValidator()
    {
        RuleFor(x => x.StakeAddress).ThrowIfNull();
        RuleFor(x => x.UtxoAssets).NotNull();
    }
}