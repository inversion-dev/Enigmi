using CardanoSharp.Wallet.Models.Addresses;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Utils;
using Enigmi.Infrastructure.Services.Authentication;
using Enigmi.Messages.UserWallet;
using FluentValidation;

namespace Enigmi.Application.Handlers.User;

public class AuthenticateCommandHandler : Handler<AuthenticateCommand, AuthenticateResponse>
{
    private IAuthenticationService AuthenticationService { get; }

    public AuthenticateCommandHandler(IAuthenticationService authenticationService)
    {
        AuthenticationService = authenticationService.ThrowIfNull();
    }

    public override async Task<ResultOrError<AuthenticateResponse>> Execute(AuthenticateCommand request, CancellationToken cancellationToken)
    {
        request.ThrowIfNull();
        
        var isVerified = WalletHelper.VerifyWalletSignature(request.AddressHex, request.Payload, request.KeyHex, request.SignatureHex);
        if (isVerified)
        {
            var stakingAddress = new Address(Convert.FromHexString(request.AddressHex));
            var token = AuthenticationService.GenerateJwtToken(stakingAddress.ToString());
            return await Task.FromResult(new AuthenticateResponse(token).ToSuccessResponse());
        }

        return await Task.FromResult("Unable to verify signature".ToFailedResponse<AuthenticateResponse>());
    }
}

public class AuthenticateCommandValidator : AbstractValidator<AuthenticateCommand>
{
    public AuthenticateCommandValidator()
    {
        RuleFor(x => x.AddressHex).ThrowIfNull();
        RuleFor(x => x.Payload).ThrowIfNull();
        RuleFor(x => x.KeyHex).ThrowIfNull();
        RuleFor(x => x.SignatureHex).ThrowIfNull();
    }
}