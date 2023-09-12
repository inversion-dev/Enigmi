using Enigmi.Application.Handlers;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.Trade;
using Enigmi.Messages.Trade;
using FluentValidation;

namespace Enigmi.Application.Trade.Handlers;

public class SignTradeByInitiatingPartyCommandHandler : Handler<SignTradeByInitiatingPartyCommand, SignTradeByInitiatingPartyResponse>
{
    private IClusterClient ClusterClient { get; }

    public SignTradeByInitiatingPartyCommandHandler(IClusterClient clusterClient)
    {
        ClusterClient = clusterClient.ThrowIfNull();
    }

    public override async Task<ResultOrError<SignTradeByInitiatingPartyResponse>> Execute(SignTradeByInitiatingPartyCommand command, CancellationToken cancellationToken)
    {
        command.ThrowIfNull();
        var tradeGrain = ClusterClient.GetGrain<ITradeGrain>(command.TradeId);
        var signTradeByInitiatingPartyResponse = await tradeGrain.SignTradeByInitiatingParty(
            new Enigmi.Grains.Shared.Trade.Messages.SignTradeByInitiatingPartyCommand(command.WitnessCborHex));

        return signTradeByInitiatingPartyResponse.Transform(x => new SignTradeByInitiatingPartyResponse());
    }
}

public class SignTradeByInitiatingPartyCommandValidator : AbstractValidator<SignTradeByInitiatingPartyCommand>
{
    public SignTradeByInitiatingPartyCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty();
        RuleFor(x => x.WitnessCborHex).NotEmpty();
    }
}