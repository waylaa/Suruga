using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Suruga.FFmpeg.Helpers;
using Suruga.FFmpeg.Interop;

namespace Suruga.FFmpeg.Logging;

/// <summary>
/// Intercepts global FFmpeg log output and forwards it to an <see cref="ILogger"/> implementation.
/// </summary>
public sealed unsafe partial class FFmpegLogger
{
    private static ILogger? _logger;
    private static bool _isCreated;

    /// <summary>
    /// Initializes FFmpeg logging integration and registers the global log callback.
    /// </summary>
    /// <param name="logger">The logger used to receive FFmpeg log output.</param>
    /// <param name="isDevelopmentBuild"></param>
    /// <returns>An initialized <see cref="FFmpegLogger"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the logger has already been initialized.
    /// </exception>
    public static FFmpegLogger Initialize(ILogger<FFmpegLogger> logger, bool isDevelopmentBuild)
    {
        _logger = logger;

        if (_isCreated)
        {
            throw new InvalidOperationException("Logger is already initialized.");
        }
        
        NativeMethods.av_log_set_level(isDevelopmentBuild ? Constants.AV_LOG_TRACE : Constants.AV_LOG_INFO);
        NativeMethods.av_log_set_callback(&Log);
        
        _isCreated = true;
        return new FFmpegLogger();
    }

    /// <summary>
    /// Native callback invoked by FFmpeg for each log message.
    /// </summary>
    /// <param name="ptr">Pointer to the FFmpeg logging context (unused).</param>
    /// <param name="level">FFmpeg log level.</param>
    /// <param name="format">Format string provided by FFmpeg.</param>
    /// <param name="vl">Variable argument list pointer.</param>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Log(void* ptr, int level, byte* format, byte* vl)
    {
        ILogger? logger = _logger;
        
        if (logger is null)
        {
            return;
        }
        
        const int bufferSize = 2048;
        Span<byte> buffer = stackalloc byte[bufferSize];
        int printPrefix = 0; // Prevent FFmpeg logs starting with '[FFmpeg]'.

        int bytesWritten = NativeMethods.av_log_format_line2
        (
            ptr,
            level,
            in PointerHelper.GetReadOnlyReference(format),
            ref PointerHelper.GetReference(vl),
            ref MemoryMarshal.GetReference(buffer),
            bufferSize,
            ref printPrefix
        );

        if (bytesWritten <= 0)
        {
            return;
        }

        int length = Math.Min(bytesWritten, bufferSize - 1);
        ReadOnlySpan<byte> span = buffer[..length].TrimEnd("\r\n"u8);
        
        if (span.IsEmpty || span.IndexOfAnyExcept(" \t\r\n"u8) < 0)
        {
            return;
        }
        
        // FFmpeg incorrectly reports this as an error at EOF when flushing the decoder.
        if (span.IndexOf("Error parsing Opus packet header."u8) >= 0)
        {
            return;
        }

        if (span.StartsWith((byte)'['))
        {
            int closingBracketIndex = span.IndexOf((byte)']');

            if (closingBracketIndex >= 0)
            {
                span = span[(closingBracketIndex + 1)..].TrimStart(" \t"u8);
            }
        }

        string message = Encoding.UTF8.GetString(span);
        Log(logger, FromFFmpegLogLevel(level), message);
    }

    /// <summary>
    /// Converts an FFmpeg log level into a <see cref="LogLevel"/>.
    /// </summary>
    /// <param name="level">The FFmpeg log level constant.</param>
    /// <returns>
    /// The mapped <see cref="LogLevel"/>, or <see cref="LogLevel.None"/> if unrecognized.
    /// </returns>
    private static LogLevel FromFFmpegLogLevel(int level) => level switch
    {
        <= Constants.AV_LOG_FATAL   => LogLevel.Critical,
        <= Constants.AV_LOG_ERROR   => LogLevel.Error,
        <= Constants.AV_LOG_WARNING => LogLevel.Warning,
        <= Constants.AV_LOG_INFO    => LogLevel.Information,
        <= Constants.AV_LOG_DEBUG   => LogLevel.Debug,
        <= Constants.AV_LOG_TRACE   => LogLevel.Trace,
        _                           => LogLevel.None
    };
    
    [LoggerMessage(Message = "{Message}")]
    private static partial void Log(ILogger logger, LogLevel level, string message);
}
