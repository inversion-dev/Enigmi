using System.Collections.ObjectModel;
using Enigmi.Application;
using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Enigmi.Common.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;
using Microsoft.Extensions.Primitives;

namespace Enigmi.HostSetup;

public class MediatorMessageSender : IMessageSender
{
	private IServiceProvider ServiceProvider { get; }

	private ScopedInformation ScopedInformation { get; }

	public MediatorMessageSender(IServiceProvider serviceProvider, ScopedInformation scopedInformation)
	{
		ServiceProvider = serviceProvider.ThrowIfNull();
		ScopedInformation = scopedInformation.ThrowIfNull();
	}

	public async Task<IResultOrError<IResponse>> SendAsync(Stream messageStream, string messageName)
	{
		messageStream.ThrowIfNull();
		messageName.ThrowIfNullOrEmpty();

		var messageTypes = AppDomain
				.CurrentDomain
				.GetAssemblies()
				.SelectMany(o => o.GetTypes())
				.Where(o => o.FullName!.Equals(messageName, StringComparison.InvariantCultureIgnoreCase))
				.ToList();

		//TODO
		var messageType =
			messageTypes
				.SingleOrDefault();

		if (messageType == null)
			return $"Message Type '{messageName}' not found".ToFailedResponse<IResponse>();

		object? msg = await JsonSerializer.DeserializeAsync(messageStream, messageType,
			new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })!
			.ConfigureAwait(false);

		if (msg == null)
			throw new Common.Exceptions.ApplicationException("Message should not be null");

		return await SendAsync(msg).ContinueOnAnyContext();
	}

	public async Task<IResultOrError<IResponse>> SendAsync(object message)
	{
		message.ThrowIfNull();

		object envelope = message.WrapInMediatorEnvelope();

		using var scope = ServiceProvider.CreateScope();
		var logger = ServiceProvider.GetService<ILogger<MediatorMessageSender>>()!;
		var mediatr = scope.ServiceProvider.GetService<IMediator>()!;
		var scopedInformation = scope.ServiceProvider.GetService<ScopedInformation>()!;
		scopedInformation.CopyFrom(ScopedInformation);
		scopedInformation.WithMessageName(message);

	
		var response = await mediatr.Send(envelope)
			.ConfigureAwait(false) as IResultOrError<IResponse>;

		return response!;
	}
}