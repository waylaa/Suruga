using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Logging;

/// <summary>
/// Intercepts global FFmpeg log output and forwards it to an <see cref="ILogger"/> implementation.
/// </summary>
/// <remarks>
/// This class configures FFmpeg's native logging system by registering a callback via
/// <see cref="av_log_set_callback(av_log_set_callback_callback_func)"/>. Log messages are
/// received from unmanaged code, converted to managed strings, filtered, and then forwarded
/// to <see cref="ILogger"/>.
/// <para>
/// The logger is initialized once and stored statically because FFmpeg requires a stable callback
/// for the lifetime of the process.
/// </para>
/// </remarks>
internal sealed unsafe partial class FFmpegLogger
{
    // av_log_set_callback requires a stable function pointer, store it
    // in this static field so the instance to LogCallback will not get garbage collected.
    private static readonly av_log_set_callback_callback Callback = LogCallback;
    
    private static ILogger? _logger;
    private static bool _isCreated;

    /// <summary>
    /// Initializes FFmpeg logging integration and registers the global log callback.
    /// </summary>
    /// <param name="logger">The logger used to receive FFmpeg log output.</param>
    /// <returns>An initialized <see cref="FFmpegLogger"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the logger has already been initialized.
    /// </exception>
    internal static FFmpegLogger Initialize(ILogger<FFmpegLogger> logger)
    {
        _logger = logger;

        if (_isCreated)
        {
            throw new InvalidOperationException("Logger is already initialized.");
        }
        
        av_log_set_level(Program.IsDevelopmentBuild ? AV_LOG_DEBUG : AV_LOG_INFO);
        av_log_set_callback(Callback);
        
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
    private static void LogCallback(void* ptr, int level, string format, byte* vl)
    {
        if (_logger is null)
        {
            return;
        }
        
        const int bufferSize = 2048;
        byte* pBuffer = stackalloc byte[bufferSize];
        int printPrefix = 0; // Prevents FFmpeg logs starting with '[FFmpeg]'.

        int bytesWritten = av_log_format_line2(ptr, level, format, vl, pBuffer, bufferSize, &printPrefix);

        if (bytesWritten <= 0)
        {
            return;
        }

        ReadOnlySpan<byte> buffer = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pBuffer);
        string message = Encoding.UTF8.GetString(buffer).Trim(); // Trim may not be needed.
        
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

        // FFmpeg incorrectly reports this as an error at EOF during decoder flushing.
        // This message is safely ignored.
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
        <= AV_LOG_FATAL   => LogLevel.Critical,
        <= AV_LOG_ERROR   => LogLevel.Error,
        <= AV_LOG_WARNING => LogLevel.Warning,
        <= AV_LOG_INFO    => LogLevel.Information,
        <= AV_LOG_DEBUG   => LogLevel.Debug,
        <= AV_LOG_TRACE   => LogLevel.Trace,
        _                 => LogLevel.None
    };
    
    [LoggerMessage(Message = "{Message}")]
    private static partial void Log(ILogger logger, LogLevel level, string message);
}
