using Enigmi.Common.Messaging;

namespace Enigmi.Common;

public static class ResultOrErrorExtensions
{
	public static object GetResultOrError<TResult>(this IResultOrError<TResult> resultOrError)
	{
		resultOrError.ThrowIfNull();

		if (!resultOrError.HasErrors && resultOrError.Result == null)
		{
			throw new ArgumentException($"Response does not have a {nameof(resultOrError.Result)} or any {nameof(resultOrError.Errors)}");
		}

		if (resultOrError.HasErrors)
			return GetErrorMessage(resultOrError.Errors);
		else
			return resultOrError.Result!;
	}

	private static ErrorMessage GetErrorMessage(IEnumerable<string> errors)
	{
		return new ErrorMessage("Internal Server Error", string.Join(Environment.NewLine, errors));
	}

	public static ResultOrError<TResult> ToSuccessResponse<TResult>(this TResult result)
	{
		return ResultOrError<TResult>.Create(result);
	}

	public static ResultOrError<TOut> Transform<TOut,TInt>(this IResultOrError<TInt> resultOrError, Func<TInt,TOut> func)
    {
        if (resultOrError.HasErrors)
        {
            return resultOrError.Errors.ToFailedResponse<TOut>();
        }

        var result = resultOrError.Result;
        result.ThrowIfNull();

        return (func(result)).ToSuccessResponse();
    }

    public static ResultOrError<TResult> ToFailedResponse<TResult>(this IEnumerable<string> errors)
    {
		errors.ThrowIfNull();

		return ResultOrError<TResult>.Create()
			.AddErrors(errors);
	}

	public static ResultOrError<TResult> ToFailedResponse<TResult>(this string error)
	{
		error.ThrowIfNullOrWhitespace();

		return ResultOrError<TResult>.Create()
			.AddError(error);
	}

	public static ResultOrError<TResult> AddError<TResult>(this ResultOrError<TResult> resultOrError, string error)
	{
		resultOrError.ThrowIfNull();
		error.ThrowIfNullOrWhitespace();

		resultOrError.Errors.Add(error);

		return resultOrError;
	}

	public static ResultOrError<TResult> AddErrors<TResult>(this ResultOrError<TResult> resultOrError, IEnumerable<string> errors)
	{
		resultOrError.ThrowIfNull();
		errors.ThrowIfNull();

		resultOrError.Errors.AddRange(errors);

		return resultOrError;
	}
}