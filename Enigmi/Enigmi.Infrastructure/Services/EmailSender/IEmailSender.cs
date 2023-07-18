namespace Enigmi.Infrastructure.Services
{
	public interface IEmailSender
	{
		Task SendEmailAsync(string toEmailAddress, string subject, string message);
	}
}