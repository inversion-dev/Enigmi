using Enigmi.Common.Domain;

namespace Enigmi.Common;

public static class IMiniLogExtensions
{
	public static void WriteToMiniLog(this IMiniLog miniLog, string? log)
	{
		miniLog.ThrowIfNull();

		if (!string.IsNullOrWhiteSpace(log))
		{
			miniLog.MiniLog = $"{DateTime.UtcNow.ToString("o")}|{log}{Environment.NewLine}-----{Environment.NewLine}{miniLog.MiniLog}".Truncate(2000);
		}
		else
		{
			miniLog.MiniLog = null;
		}
	}

	public static void ClearMiniLog(this IMiniLog miniLog)
	{
		miniLog.WriteToMiniLog(null);
	}
}