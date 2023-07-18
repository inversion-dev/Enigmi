namespace Enigmi.Common.Extensions;

public static class ConditionalExtensions
{
    public static bool IsIn<T>(this T value, params T[] collection)
    {
        value.ThrowIfNull();
        return collection.Contains(value);
    }

    public static bool IsIn<T>(this T value, IEnumerable<T> collection)
    {
        value.ThrowIfNull();
        collection.ThrowIfNull();
        return collection.Contains(value);
    }
}