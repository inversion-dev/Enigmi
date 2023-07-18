using System.Security.Claims;

namespace Enigmi.Common;

public static class ClaimsPrincipalExtensions
{
	public static string? GetName(this ClaimsPrincipal? principal)
	{
		return principal?.Claims
			.SingleOrDefault(o => o.Type.InvariantIgnoreCaseEquals(ClaimTypes.Name))?
			.Value;
	}
}