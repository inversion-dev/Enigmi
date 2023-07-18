using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Infrastructure.Services.Authentication;
using MediatR;
using static Enigmi.Common.Messaging.Enums;

namespace Enigmi.Application.Behaviors;

public sealed class ApplyMessageAccessPolicyBehavior<TRequest, TResponse> : Behavior<TRequest, TResponse>
	where TRequest : IMessage<TResponse>
	where TResponse : IResponse
{
	private IAuthenticationService AuthenticationService { get; }
	private ScopedInformation ScopedInformation { get; }

	public ApplyMessageAccessPolicyBehavior(ScopedInformation scopedInformation, IAuthenticationService authenticationService)
	{
		AuthenticationService = authenticationService;
		ScopedInformation = scopedInformation.ThrowIfNull();
	}

	public override async Task<ResultOrError<TResponse>> Execute(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<ResultOrError<TResponse>> next)
	{
		if (ScopedInformation.Headers != null)
		{
			var claimsPrincipal = AuthenticationService.ValidateJwtIfPresent(ScopedInformation.Headers);
			if (claimsPrincipal != null)
			{
				ScopedInformation.WithClaimsPrincipal(claimsPrincipal);
			}
		}

		UserContext? userContext = request.AccessMechanism switch
		{
            AccessMechanism.Anonymous => new AnonymousUserContext(ScopedInformation.ClaimsPrincipal?.GetName()),

            AccessMechanism.Authorized => new AuthorizedUserContext(ScopedInformation.ClaimsPrincipal.GetName()
			?? throw new UnauthorizedAccessException()),

			_ => throw new Common.Exceptions.ApplicationException($"{nameof(request)} '{request.AccessMechanism}' not supported")
		};

		ScopedInformation
			.WithUserContext(userContext);

		var result = await next()
			.ContinueOnAnyContext();

		return result;
	}
}