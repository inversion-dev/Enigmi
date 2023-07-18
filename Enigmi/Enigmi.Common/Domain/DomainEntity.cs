using Newtonsoft.Json;

namespace Enigmi.Common.Domain;
public abstract class DomainEntity : IModifiable, ICreatable
{
	[JsonProperty]
    public DateTime CreatedUtcTimestamp { get; private set; } = DateTime.UtcNow;

	[JsonProperty]
    public byte[] RowVersion { get; private set; } = null!;

	[JsonProperty]
    public string LastModifiedByFullName { get; private set; } = null!;

	[JsonProperty]
    public DateTime LastModifiedUtcTimestamp { get; private set; } = default(DateTime);

	private List<DomainEvent> _domainEvents = new();
    private List<WrappedDomainEvent> _sentEvents = new();
    private List<WrappedDomainEvent> _processedEvents = new();

    [JsonProperty]
    public IEnumerable<DomainEvent> DomainEvents
    {
        get { return _domainEvents.AsReadOnly(); }
        private set { _domainEvents = value.ToList(); }
    }

    [JsonProperty]
    public IEnumerable<WrappedDomainEvent> SentEvents
    {
        get { return _sentEvents.AsReadOnly(); }
        private set { _sentEvents = value.ToList(); }
    }

    [JsonProperty]
    public IEnumerable<WrappedDomainEvent> ProcessedDomainEvents
    {
        get { return _processedEvents.AsReadOnly(); }
        private set { _processedEvents = value.ToList(); }
    }


    protected void RaiseEvent(DomainEvent @event)
	{
		@event.ThrowIfNull();
        _domainEvents.Add(@event);
	}
    
    public void MarkEventAsSent(DomainEvent @event)
    {
        @event.ThrowIfNull();
        _domainEvents.Remove(@event);
        _sentEvents.Add(new WrappedDomainEvent()
        {
            DomainEvent = @event,
            UtcTimestamp = DateTime.UtcNow,
        });
    }

	public void SetModifiedBy(string fullName, DateTime timeStamp)
	{
		LastModifiedByFullName = fullName;
		LastModifiedUtcTimestamp = timeStamp;
	}

    public void MarkEventAsProcessed(DomainEvent @event)
    {
        _processedEvents.Add(new WrappedDomainEvent
        {
            DomainEvent = @event, 
            UtcTimestamp = DateTime.UtcNow
        });
    }
}

public class WrappedDomainEvent
{
    public DomainEvent DomainEvent { get; set; } = null!;
    public DateTime UtcTimestamp { get; set; }
}