using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.UserWallet;
using Enigmi.Grains.Shared.UserWallet.Messages;
using FluentValidation;
using CreateOrderCommand = Enigmi.Messages.UserWallet.CreateOrderCommand;
using CreateOrderResponse = Enigmi.Messages.UserWallet.CreateOrderResponse;

namespace Enigmi.Application.UserWallet.Handlers;

public class CreateOrderCommandHandler : Handler<CreateOrderCommand, CreateOrderResponse>
{
    private IClusterClient ClusterClient { get; }

    public CreateOrderCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }
    
    public override async Task<ResultOrError<CreateOrderResponse>> Execute(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var userWalletGrain = ClusterClient.GetGrain<IUserWalletGrain>(request.StakeAddress);
        await userWalletGrain.UpdateWalletState(new UpdateUserWalletStateCommand(request.UtxoAssets));
        var response = await userWalletGrain.CreateOrder(new Enigmi.Grains.Shared.UserWallet.Messages.CreateOrderCommand(request.PaymentAddress, request.CollectionId, request.PuzzleSize, request.Quantity));
        return response.Transform(o => new CreateOrderResponse(o.OrderId,o.UnsignedTransactionCborHex,o.Fee,o.Warning));
    }
}

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.UtxoAssets).ThrowIfNull();
    }
}