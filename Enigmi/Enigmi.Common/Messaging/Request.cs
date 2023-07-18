namespace Enigmi.Common.Messaging;

public interface IRequestResponse : IResponse
{
}

public interface IRequest<TResponse> : IMessage<TResponse>
	where TResponse : IRequestResponse
{
}

public abstract record RequestResponse : Response, IRequestResponse
{
}

public abstract record Request<TResponse> : Message<TResponse>, IRequest<TResponse>
		where TResponse : RequestResponse
{
}

public interface ICachedRequest<TResponse> : IRequest<TResponse>
	where TResponse : IRequestResponse
{
	TimeSpan GetDuration();

	bool SkipCache { get; set; }
}