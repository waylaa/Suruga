using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Suruga.Audio.FFmpeg.Logging;

namespace Suruga.Services;

internal sealed class FFmpegLoggerService(ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        FFmpegLogger.Initialize(loggerFactory);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
