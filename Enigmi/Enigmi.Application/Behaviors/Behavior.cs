using Enigmi.Common.Messaging;
using MediatR;

namespace Enigmi.Application.Behaviors;

public abstract class Behavior<TRequest, TResponse> : IPipelineBehavior<MediatorMessageEnvelope<TRequest, TResponse>, ResultOrError<TResponse>>
	where TRequest : IMessage<TResponse>
	where TResponse : IResponse

{
	public abstract Task<ResultOrError<TResponse>> Execute(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<ResultOrError<TResponse>> next);

	public Task<ResultOrError<TResponse>> Handle(MediatorMessageEnvelope<TRequest, TResponse> request, RequestHandlerDelegate<ResultOrError<TResponse>> next, CancellationToken cancellationToken)
	{
		return Execute(request.Request, cancellationToken, next);
	}
}