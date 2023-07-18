using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Infrastructure.Extensions;
using Foundatio.Caching;
using MediatR;
using System.Text.Json;

namespace Enigmi.Application.Behaviors;

public sealed class RequestCachingBehavior<TRequest, TResponse> : Behavior<TRequest, TResponse>
	where TRequest : ICachedRequest<TResponse>
	where TResponse : IRequestResponse
{
	public ICacheClient CacheClient { get; }

	public RequestCachingBehavior(ICacheClient cacheClient)
	{
		CacheClient = cacheClient.ThrowIfNull();
	}

	public override async Task<ResultOrError<TResponse>> Execute(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<ResultOrError<TResponse>> next)
	{
		if (request.SkipCache)
			return await next();

		var cachedValue = await CacheClient.GetOrLoadAsync(JsonSerializer.Serialize(request),
			async () =>
			{
				var response = await next();
				return response;
			},
			request.GetDuration()).ContinueOnAnyContext();

		return cachedValue.Value;
	}
}