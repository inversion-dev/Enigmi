using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Enigmi.Common.Messaging;
using Enigmi.Messages.Test;
using FluentValidation;

namespace Enigmi.Application.Handlers.Test;

public class RetryTestHandler : Handler<RetryTestRequest, RetryTestResponse>
{
	public static int TryCount { get; set; }

	public RetryTestHandler()
	{
	}

	public override Task<ResultOrError<RetryTestResponse>> Execute(RetryTestRequest request, CancellationToken cancellationToken)
	{
		request.TryCount++;

		TryCount = request.TryCount++;

		if (request.ShouldTryCount < request.TryCount)
			return Task.FromResult(new RetryTestResponse().ToSuccessResponse());

		throw new Common.Exceptions.ApplicationException("Retry I must!");
	}
}

public class RetryTestRequestValidator : AbstractValidator<RetryTestRequest>
{
	public RetryTestRequestValidator()
	{
	}
}