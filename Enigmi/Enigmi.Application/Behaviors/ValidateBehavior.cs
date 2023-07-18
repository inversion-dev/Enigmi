using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Enigmi.Common.Messaging;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Enigmi.Application.Behaviors;

public sealed class ValidateBehavior<TRequest, TResponse> : Behavior<TRequest, TResponse>
	where TRequest : IMessage<TResponse>
	where TResponse : IResponse
{
	public IEnumerable<IValidator<TRequest>> RequestValidators { get; }

	public IEnumerable<IValidator<TResponse>> ResponseValidators { get; }

	public ValidateBehavior(IEnumerable<IValidator<TRequest>> requestValidators, IEnumerable<IValidator<TResponse>> responseValidators)
	{
		RequestValidators = requestValidators;
		ResponseValidators = responseValidators;
	}

	public override async Task<ResultOrError<TResponse>> Execute(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<ResultOrError<TResponse>> next)
	{
		IEnumerable<ValidationFailure> requestFailures = RequestValidators
			.Select(v => v.Validate(request))
			.SelectMany(result => result.Errors)
			.Where(f => f != null);

		if (requestFailures.Any())
		{
			throw new Common.Exceptions.ValidationException(requestFailures.Select(o => o.ErrorMessage));
		}

		var response = await next().ContinueOnAnyContext();

		if (response == null)
		{
			throw new Common.Exceptions.ApplicationException($"Response does not have a {nameof(response)}");
		}

		return response;
	}
}