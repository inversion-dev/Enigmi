using System.Collections.ObjectModel;
using Enigmi.Common.Messaging;
using Microsoft.Extensions.Primitives;

namespace Enigmi.HostSetup;

public interface IMessageSender
{
	Task<IResultOrError<IResponse>> SendAsync(Stream messageStream, string messageName);

	Task<IResultOrError<IResponse>> SendAsync(object message);
}