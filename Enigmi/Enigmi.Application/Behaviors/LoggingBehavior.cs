using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Enigmi.Common.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Enigmi.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : Behavior<TRequest, TResponse>
	where TRequest : IMessage<TResponse>
	where TResponse : IResponse
{
	private ILogger<LoggingBehavior<TRequest, TResponse>> Logger { get; }

	public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
	{
		Logger = logger.ThrowIfNull();
	}

	public override async Task<ResultOrError<TResponse>> Execute(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<ResultOrError<TResponse>> next)
	{
		Logger.LogInformation($"Handling {request.GetType().Name}");

		try
		{
			var response = await next();

			Logger.LogInformation($"Handled {request.GetType().Name}");
			return response;
		}
		catch (Exception ex)
		{
			LogException(ex);
			throw;
		}

		void LogException(Exception ex)
		{
			var details = "";
			if (ex is AggregateException)
			{
				details = String.Join(", ", ((AggregateException)ex).InnerExceptions.Select(o => o.Message));
				details = details.Truncate(2000);
			}
			var json = JsonSerializer.Serialize(request);
			json = json.Truncate(2000);
			if (ex is Common.Exceptions.ApplicationException aex && aex.LogAsWarning)
			{
                Logger.LogWarning(ex, $"{request.GetType().Name} failed: {json} {details}");
			}
			else
			{
                Logger.LogError(ex, $"{request.GetType().Name} failed: {json} {details}");
			}
		}
	}
}