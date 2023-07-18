using Enigmi.Common.Domain;

namespace Enigmi.Common.Messaging;

public record EventCommandResponse : CommandResponse
{ }

public abstract record EventCommand : Command<EventCommandResponse>
{
	public abstract DomainEvent GetDomainEvent();
}

public abstract record EventCommand<T> : EventCommand where T : DomainEvent
{
	public override Enums.AccessMechanism AccessMechanism => Enums.AccessMechanism.Anonymous;

	public override T GetDomainEvent() { return _domainEvent; }

	public readonly T _domainEvent;
	public EventCommand(T domainEvent)
	{
		_domainEvent = domainEvent.ThrowIfNull();
	}
}