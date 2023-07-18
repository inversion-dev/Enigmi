using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Messages.System;
using FluentValidation;

namespace Enigmi.Application.Handlers.System;

public class AdminPingRequestHandler : Handler<AdminPingRequest, AdminPingResponse>
{
	public AdminPingRequestHandler()
	{
	}

	public override Task<ResultOrError<AdminPingResponse>> Execute(AdminPingRequest request, CancellationToken cancellationToken)
	{
		var response = new AdminPingResponse().ToSuccessResponse();
		return Task.FromResult(response);
	}
}

public class AdminPingRequestValidator : AbstractValidator<AdminPingRequest>
{
	public AdminPingRequestValidator()
	{		
	}
}