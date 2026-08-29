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
        if (_logger is null)
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

        int actualLength = Math.Min(bytesWritten, bufferSize - 1);

        ReadOnlySpan<byte> span = buffer[..actualLength];
        string message = Encoding.UTF8.GetString(span);
        
        // Trim the prefix and pointer address.
        if (message.StartsWith('['))
        {
            int closingBracketIndex = message.IndexOf(']');
            
            if (closingBracketIndex >= 0)
            {
                message = message[(closingBracketIndex + 1)..].TrimStart();
            }
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // FFmpeg incorrectly reports this as an error at EOF during decoder flushing. Ignore.
        if (message.Contains("Error parsing Opus packet header."))
        {
            return; 
        }
        
        Log(_logger, FromFFmpegLogLevel(level), message);
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
