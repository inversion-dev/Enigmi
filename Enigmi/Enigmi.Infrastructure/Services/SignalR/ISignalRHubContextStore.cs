using Microsoft.Azure.SignalR.Management;

namespace Enigmi.Infrastructure.Services.SignalR;

public interface ISignalRHubContextStore
{
    public ServiceHubContext? MessageHubContext { get; }
}