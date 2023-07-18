using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;

namespace Enigmi.Application.UserWallet.Handlers;

public class ApproveOrderCommandHandler : Handler<ApproveOrderCommand, ApproveOrderResponse>
{
    private IClusterClient ClusterClient { get; }

    public ApproveOrderCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }
    
    public override async Task<ResultOrError<ApproveOrderResponse>> Execute(ApproveOrderCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakeAddress);
        
        var response = await userWalletGrain.ApproveOrder(new Enigmi.Grains.Shared.UserWallet.Messages.ApproveOrderCommand(request.OrderId, request.WitnessCbor));
        return response.Transform(o => new ApproveOrderResponse());
    }
}

public class ApproveOrderCommandValidator : AbstractValidator<ApproveOrderCommand>
{
    public ApproveOrderCommandValidator()
    {
        RuleFor(x => x.StakeAddress).ThrowIfNull();
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.WitnessCbor).ThrowIfNull();
    }
}