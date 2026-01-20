using System.Text;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg.Logging;

internal static unsafe class FFmpegLogger
{
    private static readonly av_log_set_callback_callback _callback = LogCallback;

    private static ILogger? _logger;

    public static void Initialize(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("FFmpeg");

        av_log_set_level(AV_LOG_DEBUG);
        av_log_set_callback(_callback);
    }

    private static unsafe void LogCallback(void* ptr, int level, string format, byte* vl)
    {
        ILogger? logger = _logger;

        if (logger is null)
        {
            return;
        }

        LogLevel logLevel = MapFFmpegLogLevel(level);

        if (!logger.IsEnabled(logLevel))
        {
            return;
        }

        const int BufferSize = 1024;
        byte* buffer = stackalloc byte[BufferSize];
        int printPrefix = 1;

        int rc = av_log_format_line2(ptr, level, format, vl, buffer, BufferSize, &printPrefix);

        if (rc <= 0)
        {
            return;
        }

        int len = Math.Min(rc, BufferSize);
        string message = Encoding.UTF8.GetString(buffer, len).TrimEnd();

        logger.Log(logLevel, "[FFmpeg] {Message}", message);
    }

    private static LogLevel MapFFmpegLogLevel(int level) => level switch
    {
        <= AV_LOG_PANIC => LogLevel.Critical,
        <= AV_LOG_FATAL => LogLevel.Critical,
        <= AV_LOG_ERROR => LogLevel.Error,
        <= AV_LOG_WARNING => LogLevel.Warning,
        <= AV_LOG_INFO => LogLevel.Information,
        <= AV_LOG_VERBOSE => LogLevel.Debug,
        <= AV_LOG_DEBUG => LogLevel.Trace,
        _ => LogLevel.Trace
    };
}
