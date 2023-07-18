using Microsoft.Extensions.Primitives;
using System.Text.RegularExpressions;

namespace Enigmi.Common;

public static class StringExtensions
{
	private static readonly Regex _constraintNameWordSeparator = new Regex(@"[^_\p{L}]+", RegexOptions.None, Constants.RegexTimeout);

	public static bool ContainsDbConstraintName(this string str, string constraintName)
	{
		if (str == null)
			throw new ArgumentNullException(nameof(str));
		if (constraintName == null)
			throw new ArgumentNullException(nameof(constraintName));

		return _constraintNameWordSeparator.Split(str).Any(x => x.Equals(constraintName, StringComparison.InvariantCulture));
	}

	public static bool InvariantEquals(this string value1, string value2)
	{
		return string.Equals(value1, value2, StringComparison.InvariantCulture);
	}

	public static bool InvariantIgnoreCaseEquals(this string value1, string value2)
	{
		return string.Equals(value1, value2, StringComparison.InvariantCultureIgnoreCase);
	}

	public static bool InvariantEquals(this StringSegment value1, string value2)
	{
		if (value1 == null && value2 != null) return false;

		return value1.Equals(value2, StringComparison.InvariantCulture);
	}

	public static bool InvariantIgnoreCaseEquals(this StringSegment value1, string value2)
	{
		if (value1 == null && value2 != null) return false;

		return value1.Equals(value2, StringComparison.InvariantCultureIgnoreCase);
	}

	public static bool InvariantContains(this string value1, string value2)
	{
		return value1.Contains(value2, StringComparison.InvariantCulture);
	}

	public static bool InvariantIgnoreCaseContains(this string value1, string value2)
	{
		return value1.Contains(value2, StringComparison.InvariantCultureIgnoreCase);
	}

	public static bool InvariantContains(this IEnumerable<string> source, string value)
	{
		return source.Contains(value, StringComparer.InvariantCulture);
	}

	public static bool InvariantIgnoreCaseContains(this IEnumerable<string> source, string value)
	{
		return source.Contains(value, StringComparer.InvariantCultureIgnoreCase);
	}

	public static bool InvariantStartsWith(this string source, string value)
	{
		return source.StartsWith(value, StringComparison.InvariantCulture);
	}

	public static bool InvariantIgnoreCaseStartsWith(this string source, string value)
	{
		return source.StartsWith(value, StringComparison.InvariantCultureIgnoreCase);
	}

	public static bool InvariantEndsWith(this string source, string value)
	{
		return source.EndsWith(value, StringComparison.InvariantCulture);
	}

	public static bool InvariantIgnoreCaseEndsWith(this string source, string value)
	{
		return source.EndsWith(value, StringComparison.InvariantCultureIgnoreCase);
	}

	public static string InvariantReplace(this string value, string oldValue, string newValue)
	{
		return value.Replace(oldValue, newValue, StringComparison.InvariantCulture);
	}

	public static string InvariantIgnoreReplace(this string value, string oldValue, string newValue)
	{
		return value.Replace(oldValue, newValue, StringComparison.InvariantCultureIgnoreCase);
	}

	public static string InsertSpacesBeforeUppercases(this string value)
	{
		if (value == null)
			return "";

		return Regex.Replace(value, @"(?<=.{1})[A-Z]", " $&", RegexOptions.None, Constants.RegexTimeout);
	}

	public static string Truncate(this string value, int length)
	{
		if (value.IsNullOrWhitespace())
			return value;

		if (value.Length <= length)
			return value;

		return value.Substring(0, length);
	}

	public static bool IsNullOrWhitespace(this string? value)
	{
		return string.IsNullOrWhiteSpace(value);
	}

	public static string ToCsv(this string[] strings)
	{
		return string.Join(",", strings.ThrowIfNull());
	}
}