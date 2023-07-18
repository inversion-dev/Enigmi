using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Messages.System;
using FluentValidation;

namespace Enigmi.Application.Handlers.System;

public class GetSettingsRequestHandler : Handler<GetSettingsRequest, GetSettingsResponse>
{
	public GetSettingsRequestHandler()
	{
	}

	public override Task<ResultOrError<GetSettingsResponse>> Execute(GetSettingsRequest request, CancellationToken cancellationToken)
	{
		var response = new GetSettingsResponse(
			new GetSettingsResponse.Settings()
			{
			}).ToSuccessResponse();
		return Task.FromResult(response);
	}
}

public class GetSettingsRequestValidator : AbstractValidator<GetSettingsRequest>
{
	public GetSettingsRequestValidator()
	{		
	}
}