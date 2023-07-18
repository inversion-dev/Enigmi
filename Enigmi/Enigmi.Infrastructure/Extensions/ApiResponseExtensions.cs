using Enigmi.Common;
using Refit;

namespace Enigmi.Infrastructure.Extensions;

public static class ApiResponseExtensions
{
	public static T ResultOrThrow<T>(this ApiResponse<T> response)
	{
		response.ThrowIfNull();
		if (response.Error != null)
		{
			throw response.Error;
		}
		response.Content.ThrowIfNull();
		return response.Content!;
	}
}