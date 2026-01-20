using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Suruga.Services;

namespace Suruga.Audio.FFmpeg.Logging;

internal static class FFmpegLoggerExtensions
{
    internal static ILoggingBuilder AddFFmpeg(this ILoggingBuilder builder)
    {
        builder.Services.AddHostedService<FFmpegLoggerService>();
        return builder;
    }
}
