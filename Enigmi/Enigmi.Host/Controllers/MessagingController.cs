using System.Net;
using System.Text;
using System.Text.Json;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.HostSetup;
using Enigmi.Infrastructure.Services.Authentication;
using Enigmi.Infrastructure.Services.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Enigmi.Host.Server.Controllers;

[ApiController]
public class MessagingController : ControllerBase
{
	private ILogger<MessagingController> Logger { get; }
	private IAuthenticationService AuthenticationService { get; }
	private IMessageSender MessageSender { get; }
	private ServiceHubContext HubContext { get; }
	private ScopedInformation ScopedInformation { get; }

	public MessagingController( 
		IMessageSender messageSender,
		ISignalRHubContextStore signalRHubContext,
		IAuthenticationService authenticationService,
		ScopedInformation scopedInformation,
		ILogger<MessagingController> logger)
	{
		Logger = logger;
		AuthenticationService = authenticationService.ThrowIfNull();
		MessageSender = messageSender.ThrowIfNull();
		HubContext = signalRHubContext.MessageHubContext.ThrowIfNull();
		MessageSender = messageSender.ThrowIfNull();
		ScopedInformation = scopedInformation.ThrowIfNull();
		
	}

	[HttpPost]
	[Route("send-message")]
	public async Task<IActionResult> SendMessage()
	{
		if (Request.Headers.TryGetValue("MessageName", out var messageName))
		{
			var headers = Request.Headers.ToDictionary(x => x.Key, x => x.Value.AsEnumerable()).AsReadOnly();
			ScopedInformation.WithHeaders(headers);

			try
			{
				IResultOrError<IResponse> response = await MessageSender.SendAsync(Request.Body, messageName!);
				object responseOrError = response.GetResultOrError();
				if (responseOrError is ErrorMessage errorMessage)
				{
					return BadRequest(errorMessage.Message);
				}

				return Ok(response);
				
			}
			catch (UnauthorizedAccessException)
			{
				return StatusCode(StatusCodes.Status401Unauthorized);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex.Message, ex);
				return StatusCode(StatusCodes.Status500InternalServerError);
			}
		}

		return StatusCode(StatusCodes.Status400BadRequest);
	}

	[HttpPost]
	[Route("negotiate")]
	public async Task<IActionResult> NegotiateSignalRConnection()
	{
		var headers = Request.Headers.ToDictionary(x => x.Key, x => x.Value.AsEnumerable()).AsReadOnly();
		var principal = AuthenticationService.ValidateJwtIfPresent(headers);
		if (principal == null)
		{
			return Unauthorized();
		}
		
		var negotiateResponse = await HubContext.NegotiateAsync(new() { UserId = principal.GetName() });

		return new JsonResult(new Dictionary<string, string>()
		{
			{ "url", negotiateResponse.Url! },
			{ "accessToken", negotiateResponse.AccessToken! }
		});
	}
	
	[HttpGet]
	[Route("test")]
	public async Task<IActionResult> TestSignalR()
	{
		await HubContext.Clients.Users("stake_test1uz49d5r7szs8rr2ejv8xmn6udca29s959y8pl4dl06jt7gq0penuw").SendAsync("clientMessage", "user specific message");
		await HubContext.Clients.All.SendAsync("clientMessage", "test message");
		return Ok();
	}
}