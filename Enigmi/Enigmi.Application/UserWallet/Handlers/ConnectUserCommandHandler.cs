using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;
using GrainCommand = Enigmi.Grains.Shared.UserWallet.Messages;

namespace Enigmi.Application.UserWallet.Handlers;

public class ConnectUserCommandHandler : Handler<ConnectUserCommand, UserConnectedResponse>
{
    private IClusterClient ClusterClient { get; }

    public ConnectUserCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<UserConnectedResponse>> Execute(ConnectUserCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakeAddress);
        var response = await userWalletGrain.Connect(new GrainCommand.ConnectUserCommand(request.UtxoAssets));
        return response.Transform(o => new UserConnectedResponse());
    }
}

public class ConnectUserCommandValidator : AbstractValidator<ConnectUserCommand>
{
    public ConnectUserCommandValidator()
    {
        RuleFor(x => x.UtxoAssets).NotNull();
    }
}