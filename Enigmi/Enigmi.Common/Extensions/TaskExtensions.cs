using System.Runtime.CompilerServices;

namespace Enigmi.Common;

public static class TaskExtensions
{
	public static ConfiguredTaskAwaitable ContinueOnAnyContext(this Task task)
	{
		task.ThrowIfNull();
		return task.ConfigureAwait(false);
	}

	public static ConfiguredTaskAwaitable<T> ContinueOnAnyContext<T>(this Task<T> task)
	{
		task.ThrowIfNull();
		return task.ConfigureAwait(false);
	}

	public static ConfiguredValueTaskAwaitable ContinueOnAnyContext(this ValueTask task)
	{
		task.ThrowIfNull();
		return task.ConfigureAwait(false);
	}

	public static ConfiguredValueTaskAwaitable<T> ContinueOnAnyContext<T>(this ValueTask<T> task)
	{
		task.ThrowIfNull();
		return task.ConfigureAwait(false);
	}
}