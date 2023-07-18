using Enigmi.Common;
using Enigmi.Common.Messaging;
using FluentValidation.Results;

namespace Enigmi.Application;

internal static class ResultOrErrorExtensions
{
	public static ResultOrError<TResult> ToFailedResponse<TResult>(this IEnumerable<ValidationFailure> validationFailures)
	   where TResult : IResponse
	{
		validationFailures.ThrowIfNull();

		return ResultOrError<TResult>.Create()
			.AddErrors(validationFailures.Select(o => o.ErrorMessage));
	}

	public static ResultOrError<TResult> AddErrors<TResult>(this ResultOrError<TResult> resultOrError, IEnumerable<ValidationFailure> validationFailures)
	   where TResult : IResponse
	{
		resultOrError.ThrowIfNull();
		validationFailures.ThrowIfNull();

		resultOrError.Errors.AddRange(validationFailures.Select(o => o.ErrorMessage));

		return resultOrError;
	}
}