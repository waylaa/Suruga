using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Suruga.Common;
using Suruga.FFmpeg.Helpers;
using Suruga.FFmpeg.Interop;

namespace Suruga.FFmpeg.Logging;

public sealed unsafe class FFmpegLogger
{
    private static bool _isCreated;
    
    public static FFmpegLogger Initialize(bool isDevelopmentBuild)
    {
        if (_isCreated)
        {
            throw new InvalidOperationException("FFmpeg logger is already initialized.");
        }
        
        NativeMethods.av_log_set_level(isDevelopmentBuild ? Constants.AV_LOG_DEBUG : Constants.AV_LOG_INFO);
        NativeMethods.av_log_set_callback(&Log);
        
        _isCreated = true;
        return new FFmpegLogger();
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Log(void* ptr, int level, byte* format, byte* vl)
    {
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
        
        // Do not include pointer addresses in logs.
        if (span.StartsWith((byte)'['))
        {
            int closingBracketIndex = span.IndexOf((byte)']');

            if (closingBracketIndex >= 0)
            {
                span = span[(closingBracketIndex + 1)..].TrimStart(" \t"u8);
            }
        }

        string message = Encoding.UTF8.GetString(span);
        Logger.Log<FFmpegLogger>(FromFFmpegLogLevel(level), message);
    }
    
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
}
