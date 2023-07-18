using Enigmi.Common.Messaging;

namespace Enigmi.Application;

public class MediatorMessageEnvelope<TRequest, TResponse> : MediatR.IRequest<ResultOrError<TResponse>>
	where TRequest : IMessage<TResponse>
	where TResponse : IResponse
{
	public MediatorMessageEnvelope(TRequest request)
	{
		this.Request = request;
	}

	public TRequest Request { get; }
}