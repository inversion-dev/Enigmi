using Enigmi.Common;
using Polly;

namespace Enigmi.Infrastructure.Extensions;

public static class PollyExtensions
{
	public static T ResultOrFinalOrThrow<T>(this PolicyResult<T> policyResult)
	{
		policyResult.ThrowIfNull();
		if (policyResult!.Result != null)
			return policyResult.Result;
		if (policyResult.FinalHandledResult != null)
			return policyResult.FinalHandledResult;
		policyResult.FinalException.ThrowIfNull();
		throw policyResult.FinalException;
	}
}