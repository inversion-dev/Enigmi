using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Enigmi.Common.Messaging;
using Enigmi.HostSetup;

namespace Enigmi.InternalHost;

public abstract class FunctionBase
{
	private IMessageSender MessageSender { get; }

	protected FunctionBase(IMessageSender messageSender)
	{
		MessageSender = messageSender.ThrowIfNull();
	}

	protected async Task<TCommandResponse> SendCommandAsync<TCommandResponse>(ICommand<TCommandResponse> command)
		where TCommandResponse : ICommandResponse
	{
		command.ThrowIfNull();

		var commandResult = await MessageSender.SendAsync(command)
				.ContinueOnAnyContext();

		if (commandResult.HasErrors)
		{
			throw new Common.Exceptions.ApplicationException(string.Join(Environment.NewLine, (IEnumerable<string>)commandResult.Errors));
		}

		return (TCommandResponse)commandResult.Result;
	}

	protected async Task SendAllAsync<TRequestResponse, TCommandResponse>(IRequest<TRequestResponse> request, Func<TRequestResponse, IEnumerable<ICommand<TCommandResponse>>> commandTransform)
		where TRequestResponse : IRequestResponse
		where TCommandResponse : ICommandResponse
	{
		request.ThrowIfNull();
		commandTransform.ThrowIfNull();

		var requestResponse = await MessageSender.SendAsync(request)
				.ContinueOnAnyContext();

		if (requestResponse.HasErrors)
		{
			throw new Common.Exceptions.ApplicationException(string.Join(Environment.NewLine, (IEnumerable<string>)requestResponse.Errors));
		}

		var commands = commandTransform((TRequestResponse)requestResponse.Result).ToArray();

		var commandResults = new List<IResultOrError<IResponse>>(commands.Length);
		foreach (var command in commands)
		{
			try
			{
				var result = await MessageSender.SendAsync(command).ContinueOnAnyContext();
				commandResults.Add(result);
			}
			catch (Exception ex)
			{
				commandResults.Add(ex.ToString().ToFailedResponse<IResponse>());
			}
		}

		if (commandResults.Any(x => x.HasErrors))
		{
			var errors = commandResults.Where(x => x.HasErrors).SelectMany(x => x.Errors);
			throw new Common.Exceptions.ApplicationException(string.Join(Environment.NewLine, errors));
		}
	}
}