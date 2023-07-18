using Enigmi.Common;
using Enigmi.Common.Exceptions;
using Enigmi.Common.Messaging;
using Enigmi.Messages.Test;

namespace Enigmi.Application;

public static class MessageExtensions
{
	public static object WrapInMediatorEnvelope(this object msg)
	{
		msg.ThrowIfNull();
		//TODO: IMPROVE THIS
		var enevelopeGenericArguments = new[] { msg.GetType() }
			.Concat(msg.GetType().GetGenericAbstractClassOf(typeof(Message<>))!.GetGenericArguments());
		Type envelopeType = typeof(MediatorMessageEnvelope<,>).MakeGenericType(enevelopeGenericArguments.ToArray());

		object envelope = Activator.CreateInstance(envelopeType, msg)!;
		return envelope;
	}

	public static bool ShouldRetry(this IMessage message, Exception ex, int retryCount)
	{
		message.ThrowIfNull();
		ex.ThrowIfNull();

		if (retryCount > 2)
			return false;

		return message switch
		{
			RetryTestRequest msg => ShouldRetryRetryTestRequest(msg),
			_ => ShouldRetryDefault(message)
		};

		bool ShouldRetryRetryTestRequest(RetryTestRequest message)
		{
			return ShouldRetryDefault(message) || (ex is Common.Exceptions.ApplicationException artBankException && artBankException.Message.Contains("Retry I must!"));
		}

		bool ShouldRetryDefault(IMessage message)
		{
			return false;
		}
	}
}