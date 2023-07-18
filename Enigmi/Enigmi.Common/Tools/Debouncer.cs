namespace Enigmi.Common.Tools;

public static class Debouncer
{
	public static Action<T> DebounceEvent<T>(Action<T> action, TimeSpan interval)
	{
		action.ThrowIfNull();
		interval.ThrowIfNull();

		return Debounce<T>(arg =>
		{
			action.Invoke(arg);
		}, interval);
	}

	public static Action<T> Debounce<T>(Action<T> action, TimeSpan interval)
	{
		action.ThrowIfNull();
		interval.ThrowIfNull();

		var last = 0;
		return arg =>
		{
			var current = Interlocked.Increment(ref last);
			Task.Delay(interval).ContinueWith(task =>
			{
				if (current == last)
				{
					action(arg);
				}
			});
		};
	}
}