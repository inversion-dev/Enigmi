using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.HostSetup;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;

namespace Enigmi.InternalHost;

public class MessageHandlerFunction
{
	private ILogger Logger { get; }

	private IMessageSender MessageSender { get; }

	private ScopedInformation ScopedInformation { get; }

	public MessageHandlerFunction(ILogger<MessageHandlerFunction> logger, IMessageSender messageSender, ScopedInformation scopedInformation)
	{
		Logger = logger.ThrowIfNull();
		MessageSender = messageSender.ThrowIfNull();
		ScopedInformation = scopedInformation.ThrowIfNull();
	}

	[Function(nameof(MessageHandlerFunction))]
	public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req, ClaimsPrincipal claimPrincipal)
	{
		Logger.LogInformation("C# HTTP trigger function processed a request.");

		//TODO remove this once https://github.com/Azure/azure-functions-dotnet-worker/issues/789 is resolved
		claimPrincipal = claimPrincipal ?? req.ParsePrincipal();

		ScopedInformation
			.WithClaimsPrincipal(claimPrincipal);

		var messageName = req.Headers.GetValues("MessageName").FirstOrDefault();

		IResultOrError<IResponse> response = await MessageSender.SendAsync(req.Body, messageName);
		object responseOrError = response.GetResultOrError();

		string json = JsonSerializer.Serialize(responseOrError);
		HttpResponseData httpResponse = req.CreateResponse(responseOrError is ErrorMessage ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
		httpResponse.Headers.Add("Content-Type", MediaTypeNames.Application.Json);
		httpResponse.WriteString(json);
		return httpResponse;
	}
}