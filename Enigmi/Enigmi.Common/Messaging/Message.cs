namespace Enigmi.Common.Messaging;

public interface IMessage
{
}

public interface IResponse
{
}

public interface IMessage<TResponse> : IMessage
	where TResponse : IResponse
{
	Enums.AccessMechanism AccessMechanism { get; }
}

public abstract record Response : IResponse
{
}

public abstract record Message<TResponse> : IMessage<TResponse>
		where TResponse : Response
{
	public abstract Enums.AccessMechanism AccessMechanism { get; }
}