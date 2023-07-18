namespace Enigmi.Infrastructure.Services.TimeProvider;

public interface IDateTimeProvider
{
	DateTime UtcNow { get; }
}