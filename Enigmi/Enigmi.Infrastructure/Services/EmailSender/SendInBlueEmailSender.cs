using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using static Enigmi.Common.Settings;
using static System.FormattableString;

namespace Enigmi.Infrastructure.Services;

public class SendInBlueEmailSender : IEmailSender
{
	private SendInBlueSettings Config { get; }

	private HttpClient Client { get; }

	public SendInBlueEmailSender(SendInBlueSettings config, HttpClient client)
	{
		Config = config.ThrowIfNull();
		Client = client.ThrowIfNull();
	}

	public async Task SendEmailAsync(string toEmailAddress, string subject, string message)
	{
		toEmailAddress.ThrowIfNullOrWhitespace();
		subject.ThrowIfNullOrWhitespace();
		message.ThrowIfNullOrWhitespace();
		JObject emailObject = CreateEmailObject(Config.SenderEmail, toEmailAddress, Array.Empty<string>(), $"{Config.SubjectPrefix}{subject}", message, Config.FriendlyName);

		HttpRequestMessage request = new(HttpMethod.Post, new Uri(new Uri(Config.ApiUrl), "smtp/email"));
		request.Headers.Add("api-key", Config.ApiKey);

		request.Content = new StringContent(
			emailObject.ToString(Formatting.None),
			Encoding.UTF8,
			System.Net.Mime.MediaTypeNames.Application.Json);

		HttpResponseMessage response = await Client.SendAsync(request).ContinueOnAnyContext();

		if (!response.IsSuccessStatusCode)
		{
			string responseContent = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();

			if (!string.IsNullOrEmpty(responseContent))
			{
				JObject responseJson = JObject.Parse(responseContent);
				string errorMessage = responseJson["message"]?.Value<string>() ?? "<null>";
				string errorCode = responseJson["code"]?.Value<string>() ?? "<null>";

				throw new Common.Exceptions.ApplicationException(Invariant($"Sending email failed with response status code {response.StatusCode} and response: {errorCode}: {errorMessage}"));
			}

			throw new Common.Exceptions.ApplicationException(Invariant($"Sending email failed with response status code {response.StatusCode}"));
		}
	}

	private static JObject CreateEmailObject(string senderEmailAddress, string toEmailAddress, string[] bccEmailAddresses, string subject, string messageHtml, string senderFriendlyName)
	{
		JObject jObject = new();

		JObject senderObject = new();
		senderObject.Add("email", senderEmailAddress);
		senderObject.Add("name", senderFriendlyName);

		JObject toObject = new();
		toObject.Add("email", toEmailAddress);

		JArray toArray = new();
		toArray.Add(toObject);

		JArray bccArray = new(bccEmailAddresses.Select(bccEmailAddress =>
		{
			JObject bccObject = new();
			bccObject.Add("email", bccEmailAddress);

			return bccObject;
		}));

		jObject.Add("sender", senderObject);
		jObject.Add("to", toArray);
		jObject.Add("subject", subject);
		jObject.Add("htmlContent", messageHtml);

		if (bccArray.Any())
			jObject.Add("bcc", bccArray);

		return jObject;
	}
}