using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Enigmi.Common;

public static class EnumExtensions
{
	public static string GetDisplayName(this Enum enumValue)
	{
		enumValue.ThrowIfNull();
		return enumValue.GetType()?
			.GetMember(enumValue.ToString())?
			.First()?
			.GetCustomAttribute<DisplayAttribute>()?
			.Name ?? "(unknown)";
	}
}