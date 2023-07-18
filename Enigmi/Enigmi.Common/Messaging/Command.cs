namespace Enigmi.Common.Messaging;

public interface ICommandResponse : IResponse
{
}

public interface ICommand<TResponse> : IMessage<TResponse>
	where TResponse : ICommandResponse
{
}

public abstract record CommandResponse : Response, ICommandResponse
{
}

public abstract record Command<TResponse> : Message<TResponse>, ICommand<TResponse>
		where TResponse : CommandResponse
{
}