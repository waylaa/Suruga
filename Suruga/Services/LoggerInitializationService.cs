using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Suruga.Common;

namespace Suruga.Services;

internal sealed class LoggerInitializationService(ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.Initialize(loggerFactory);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.Release();
        return Task.CompletedTask;
    }
}
