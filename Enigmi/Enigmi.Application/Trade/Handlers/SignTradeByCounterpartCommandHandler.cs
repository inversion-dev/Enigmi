using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Messages.Trade;
using FluentValidation;

namespace Enigmi.Application.Trade.Handlers;

public class SignTradeByCounterpartCommandHandler : Handler<SignTradeByCounterpartCommand, SignTradeByCounterpartResponse>
{
    private IClusterClient ClusterClient { get; }

    public SignTradeByCounterpartCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient;
    }
    public override async Task<ResultOrError<SignTradeByCounterpartResponse>> Execute(SignTradeByCounterpartCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(request.TradeId);
        var signByCounterPartyResponse = await tradeGrain.SignByCounterparty(
            new Enigmi.Grains.Shared.Trade.Messages.SignTradeByCounterpartyCommand(request.WitnessCborHex));
        
        return signByCounterPartyResponse.Transform(x => new SignTradeByCounterpartResponse());
    }
}

public class SignTradeByCounterpartCommandValidator : AbstractValidator<SignTradeByCounterpartCommand>
{
    public SignTradeByCounterpartCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty();
        RuleFor(x => x.WitnessCborHex).NotEmpty();
    }
}