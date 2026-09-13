using Microsoft.Extensions.Logging;
using Suruga.Common;
using NetCordLogLevel = NetCord.Logging.LogLevel;

namespace Suruga.Logging;

internal sealed class AudioLogger(ILogger<AudioLogger> logger) : NetCord.Logging.IVoiceLogger
{
    public bool IsEnabled(NetCordLogLevel logLevel)
        => logger.IsEnabled(Map(logLevel));
    
    public void Log<TState>
    (
        NetCordLogLevel logLevel,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        string message = formatter(state, exception);

        // Why is this even spammed on debug instead of trace is beyond me…
        if (message.Contains("Received an RTP packet with an unknown payload type"))
        {
            return;
        }

        if (message.Contains("Received an unknown opcode"))
        {
            return;
        }
        
        Logger.Log<AudioLogger>(Map(logLevel), message, exception);
    }
    
    private static LogLevel Map(NetCordLogLevel level) => level switch
    {
        NetCordLogLevel.Error => LogLevel.Error,
        NetCordLogLevel.Critical => LogLevel.Critical,
        NetCordLogLevel.Warning => LogLevel.Warning,
        NetCordLogLevel.Information => LogLevel.Information,
        NetCordLogLevel.Debug => LogLevel.Debug,
        NetCordLogLevel.Trace => LogLevel.Trace,
        _ => LogLevel.None
    };
}
