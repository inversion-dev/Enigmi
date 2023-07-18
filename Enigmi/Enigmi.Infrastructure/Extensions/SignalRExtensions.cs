using Enigmi.Common;
using Enigmi.Common.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace Enigmi.Infrastructure.Extensions;

public static class SignalRExtensions
{
    public static Task SendAsync(this IClientProxy clientProxy, ISignalRMessage message, CancellationToken cancellationToken = default)
    {
        return clientProxy.SendCoreAsync(message.GetType().Name, new object?[] { message.ThrowIfNull() }, cancellationToken);
    }
}