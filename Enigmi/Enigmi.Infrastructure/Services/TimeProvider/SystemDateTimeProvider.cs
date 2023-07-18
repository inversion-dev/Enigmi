namespace Enigmi.Infrastructure.Services.TimeProvider
{
	public class SystemDateTimeProvider : IDateTimeProvider
	{
		public DateTime UtcNow
		{
			get
			{
				return DateTime.UtcNow;
			}
		}
	}
}