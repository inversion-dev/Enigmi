using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Messages.UserWallet;
using FluentValidation;

namespace Enigmi.Application.UserWallet.Handlers;

public class GetActiveOrderRequestHandler : Handler<GetActiveOrderRequest, GetActiveOrderResponse>
{
    private IClusterClient ClusterClient { get; }

    public GetActiveOrderRequestHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }
    public override async Task<ResultOrError<GetActiveOrderResponse>> Execute(GetActiveOrderRequest request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakeAddress);
        var getActiveOrderResponse = await userWalletGrain.GetActiveOrder();

        return getActiveOrderResponse.Transform(o => new GetActiveOrderResponse(o.OrderId, o.OrderState, o.NumberOfConfirmations));
    }
}

public class GetActiveOrderRequestValidator : AbstractValidator<GetActiveOrderRequest>
{
    public GetActiveOrderRequestValidator()
    {
        RuleFor(x => x.StakeAddress).NotEmpty();
    }
}