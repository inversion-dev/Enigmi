using Enigmi.Common.Domain;
using Newtonsoft.Json;

namespace Enigmi.Common.Messaging
{
	public class CustomEventGridEvent<T> where T : DomainEvent
	{
		public string Id { get; set; }

		public string EventType { get; set; }

		public string Subject { get; set; }

		public string EventTime { get; set; }

		public T Data { get; set; }

		[JsonConstructor]
		public CustomEventGridEvent(string id, string eventType, string subject, string eventTime, T data)
		{
			Id = id;
			EventType = eventType;
			Subject = subject;
			EventTime = eventTime;
			Data = data;
		}

		public CustomEventGridEvent(T data, string eventType)
		{
			data.ThrowIfNull();

			Id = Guid.NewGuid().ToString();
			Data = data;
			EventType = eventType;
			Subject = eventType;

			DateTime localTime = DateTime.Now;
			DateTimeOffset localTimeAndOffset = new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
			EventTime = localTimeAndOffset.ToString("o");
		}
	}
}