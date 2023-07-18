namespace Enigmi.Common.Extensions;

public static class IntExtensions
{
	public static int ToInt32OrMax(this uint value)
	{
		if (value > int.MaxValue)
			return int.MaxValue;
		return Convert.ToInt32(value);
	}

	public static ulong Sum(this IEnumerable<ulong> values)
	{
		if (values == null || !values.Any())
			return 0;
		return values.Aggregate((a, b) => a + b);
	}
}