using Enigmi.Common.Messaging;
using MediatR;

namespace Enigmi.Application.Handlers;

public abstract class Handler<TRequest, TResponse>
	: IRequestHandler<MediatorMessageEnvelope<TRequest, TResponse>, ResultOrError<TResponse>>
	where TRequest : IMessage<TResponse>
	where TResponse : IResponse
{
	public virtual Task<ResultOrError<TResponse>> Handle(MediatorMessageEnvelope<TRequest, TResponse> request, CancellationToken cancellationToken)
	{
		return Execute(request.Request, cancellationToken);
	}

	public abstract Task<ResultOrError<TResponse>> Execute(TRequest request, CancellationToken cancellationToken);
}