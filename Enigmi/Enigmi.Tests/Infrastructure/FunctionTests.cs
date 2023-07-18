namespace Enigmi.Tests;

using Azure.Core.Serialization;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.HostSetup;
using Enigmi.InternalHost;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Configuration = HostSetup.ConfigurationExtensions;

/// <summary>
/// Most of this logic needs to be replaced once https://github.com/Azure/azure-functions-dotnet-worker/issues/281 has been closed
/// </summary>
public abstract class FunctionTests : DbConnectedTests
{
	protected ServiceProvider ServiceProvider { get; }

	private MessageHandlerFunction Function => new MessageHandlerFunction(new Mock<ILogger<MessageHandlerFunction>>().Object, ServiceProvider.GetRequiredService<IMessageSender>(), ScopedInformation);

	protected ScopedInformation ScopedInformation => ServiceProvider.GetRequiredService<ScopedInformation>();

	public FunctionTests()
	{
		ServiceProvider = CreateServiceProvider();
	}

	private ClaimsPrincipal? claimsPrincipal = null;

	protected void LogTestUserIn(ClaimsPrincipal? principal = null)
	{
		if (principal == null)
			principal = GetClaimsPrincipal();
		claimsPrincipal = principal;
	}

	protected async Task<TResponse> SendAsync<TResponse>(IMessage<TResponse> request)
		where TResponse : IResponse
	{
		var httpRequest = await GetHttpRequestAsync<TResponse>(request).ContinueOnAnyContext();

		var result = await Function.Run(httpRequest, claimsPrincipal);
		result.Body.Position = 0;

		if (result.StatusCode == HttpStatusCode.InternalServerError)
		{
			var errorMessage = await JsonSerializer.DeserializeAsync<ErrorMessage>(result.Body).ContinueOnAnyContext();

			throw new ErrorMessageReturnedException(errorMessage!);
		}
		else
		{
			var response = await JsonSerializer.DeserializeAsync<TResponse>(result.Body).ContinueOnAnyContext();
			return response!;
		}
	}

	private ServiceProvider CreateServiceProvider()
	{
		var config = Configuration.GetConfiguration();

		IServiceCollection serviceCollection = new ServiceCollection();

		serviceCollection.ConfigureServices(config);

		serviceCollection = OverrideMocks(serviceCollection);

		var serviceProvider = serviceCollection.BuildServiceProvider();
		return serviceProvider;
	}

	private async Task<HttpRequestData> GetHttpRequestAsync<TResponse>(object request)
		where TResponse : IResponse
	{
		FunctionContext context = GetFunctionContext();
		HttpResponseData response = GetHttpResponse(context);

		var requestMoq = new Mock<HttpRequestData>(context);

		var memoryStream = new MemoryStream();
		await JsonSerializer.SerializeAsync(memoryStream, request).ConfigureAwait(false);
		var requestHeaders = new HttpHeadersCollection();
		requestHeaders.Add("MessageName", request.GetType().FullName);

		memoryStream.Position = 0;
		requestMoq.SetupGet(o => o.Headers).Returns(requestHeaders);
		requestMoq.Setup(o => o.CreateResponse()).Returns(response);
		requestMoq.SetupGet(o => o.Body).Returns(memoryStream);

		return requestMoq.Object;
	}

	private static HttpResponseData GetHttpResponse(FunctionContext context)
	{
		var responseMoq = new Mock<HttpResponseData>(context);

		responseMoq.SetupProperty(o => o.Headers, new HttpHeadersCollection());
		responseMoq.SetupProperty(o => o.Body, new MemoryStream());
		responseMoq.SetupProperty(o => o.StatusCode, HttpStatusCode.OK);
		return responseMoq.Object;
	}

	private static FunctionContext GetFunctionContext()
	{
		var contextMoq = new Mock<FunctionContext>();
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton(Options.Create(new WorkerOptions { Serializer = new JsonObjectSerializer() }));
		var serviceProvider = serviceCollection.BuildServiceProvider();
		contextMoq.SetupProperty(o => o.InstanceServices, serviceProvider);
		return contextMoq.Object;
	}

	protected virtual ClaimsPrincipal GetClaimsPrincipal()
	{
		var identity = new ClaimsIdentity(UserIdentityIdentityProvider);
		identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, UserIdentityExternalId));
		identity.AddClaim(new Claim(ClaimTypes.Name, "test"));

		var principal = new ClaimsPrincipal(identity);
		return principal;
	}

	protected virtual string UserIdentityIdentityProvider { get; } = Guid.NewGuid().ToString();

	protected virtual string UserIdentityExternalId { get; } = Guid.NewGuid().ToString();
}