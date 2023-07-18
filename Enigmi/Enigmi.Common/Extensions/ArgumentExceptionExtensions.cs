using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Enigmi.Common;

public static class ArgumentExceptionExtensions
{
	public static T ThrowIfNull<T>([NotNull] this T? argument, [CallerArgumentExpression("argument")] string? paramName = null)
	{
		if (argument == null)
			throw new ArgumentNullException(paramName);

		return argument;
	}

	public static IEnumerable<T> ThrowIfNullOrEmpty<T>([NotNull] this IEnumerable<T>? argument, [CallerArgumentExpression("argument")] string? paramName = null)
	{
		if (argument == null)
			throw new ArgumentNullException(paramName);
		if (!argument.Any())
			throw new ArgumentException(paramName);
		return argument;
	}

	public static string ThrowIfNullOrWhitespace(this string? argument, [CallerArgumentExpression("argument")] string? paramName = null)
	{
		if (string.IsNullOrWhiteSpace(argument))
			throw new ArgumentException("string cannot be null or whitespace", paramName);

		return argument;
	}

	public static Guid ThrowIfEmpty(this Guid argument, [CallerArgumentExpression("argument")] string? paramName = null)
	{
		if (argument == Guid.Empty)
			throw new ArgumentException("guid cannot be empty", paramName);

		return argument;
	}

	public static string ThrowIfNullOrShorterThan(this string argument, uint length, [CallerArgumentExpression("argument")] string? paramName = null)
	{
		argument = argument.ThrowIfNullOrWhitespace();
		if (argument.Length < length)
			throw new ArgumentException($"string cannot be shorter than {length} characters", paramName);

		return argument;
	}

	public static T ThrowIf<T>(this T argument, Func<T, bool> checkFunc, string message = "", [CallerArgumentExpression("argument")] string? paramName = null)
	{
		if (checkFunc(argument))
			throw new ArgumentNullException(paramName, message);

		return argument;
	}
}