using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Grains.Shared.Trade.Messages;
using Enigmi.Messages.Trade;
using FluentValidation;

namespace Enigmi.Application.Trade.Handlers;

public class BuildTransactionCommandHandler : Handler<BuildTransactionCommand, BuildTransactionResponse>
{
    private IClusterClient ClusterClient { get; }

    public BuildTransactionCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }
    
    public override async Task<ResultOrError<BuildTransactionResponse>> Execute(BuildTransactionCommand command, CancellationToken cancellationToken)
    {
        command.ThrowIfNull();
        var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(command.TradeId);
        var response = await tradeGrain.BuildTransaction(new CreateTransactionCommand());
        return response.Transform(x => new BuildTransactionResponse(x.UnsignedCbor, x.Fee));
    }
}

public class BuildTransactionCommandValidator : AbstractValidator<BuildTransactionCommand>
{
    public BuildTransactionCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty();
    }
}