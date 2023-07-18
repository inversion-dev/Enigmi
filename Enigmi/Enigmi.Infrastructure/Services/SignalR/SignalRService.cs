using Enigmi.Common;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enigmi.Infrastructure.Services.SignalR;

public class SignalRService : IHostedService, ISignalRHubContextStore, IDisposable
{
    private Settings Settings { get; }
    
    private const string MessageHub = "Message";
    private ILoggerFactory? LoggerFactory { get; }

    public ServiceHubContext? MessageHubContext { get; private set; }

    public SignalRService(ILoggerFactory loggerFactory, Settings settings)
    {
        Settings = settings.ThrowIfNull();
        LoggerFactory = loggerFactory.ThrowIfNull();
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        using var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o => o.ConnectionString = Settings.SignalRConnection)
            .WithLoggerFactory(LoggerFactory)
            .BuildServiceManager();
        MessageHubContext = await serviceManager.CreateHubContextAsync(MessageHub, cancellationToken);
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(Dispose(MessageHubContext));
    }

    private static Task Dispose(ServiceHubContext? hubContext)
    {
        if (hubContext == null)
        {
            return Task.CompletedTask;
        }
        return hubContext.DisposeAsync();
    }

    public void Dispose()
    {
        MessageHubContext?.Dispose();
    }
}