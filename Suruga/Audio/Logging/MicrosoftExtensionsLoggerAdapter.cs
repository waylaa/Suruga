using Microsoft.Extensions.Logging;
using NetCord.Logging;

namespace Suruga.Audio.Logging;

/// <summary>
/// Forwards NetCord gateway, voice and rest logs to an <see cref="ILogger"/>
/// while also optionally writing them to a <see cref="TextWriter"/>.
/// </summary>
internal sealed class MicrosoftExtensionsLoggerAdapter
(
    ILogger underlyingLogger,
    TextWriter? writer = null,
    NetCord.Logging.LogLevel minimumLogLevel = NetCord.Logging.LogLevel.Information,
    IFormatProvider? formatProvider = null,
    TimeProvider? timeProvider = null
) : IGatewayLogger, IRestLogger, IVoiceLogger
{
    private readonly TextWriterLogger _textLogger = new(writer ?? TextWriter.Null, minimumLogLevel, formatProvider, timeProvider);

    public void Log<TState>(NetCord.Logging.LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Let TextWriterLogger handle standard formatting.
        ((IGatewayLogger)_textLogger).Log(logLevel, state, exception, formatter);

        if (underlyingLogger.IsEnabled(Map(logLevel)))
        {
            // Forward to Microsoft.Extensions.Logging.
            underlyingLogger.Log(Map(logLevel), exception, "{Source}: {Message}", "Gateway", formatter(state, exception));
        }
    }

    public bool IsEnabled(NetCord.Logging.LogLevel logLevel)
        => ((IGatewayLogger)_textLogger).IsEnabled(logLevel);

    void IRestLogger.Log<TState>(NetCord.Logging.LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ((IRestLogger)_textLogger).Log(logLevel, state, exception, formatter);

        if (underlyingLogger.IsEnabled(Map(logLevel)))
        {
            underlyingLogger.Log(Map(logLevel), exception, "{Source}: {Message}", "Rest", formatter(state, exception));
        }
    }

    bool IRestLogger.IsEnabled(NetCord.Logging.LogLevel logLevel)
        => ((IRestLogger)_textLogger).IsEnabled(logLevel);

    void IVoiceLogger.Log<TState>(NetCord.Logging.LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ((IVoiceLogger)_textLogger).Log(logLevel, state, exception, formatter);

        if (underlyingLogger.IsEnabled(Map(logLevel)))
        {
            underlyingLogger.Log(Map(logLevel), exception, "{Source}: {Message}", "Voice", formatter(state, exception));
        }
    }

    bool IVoiceLogger.IsEnabled(NetCord.Logging.LogLevel logLevel)
        => ((IVoiceLogger)_textLogger).IsEnabled(logLevel);

    private static Microsoft.Extensions.Logging.LogLevel Map(NetCord.Logging.LogLevel level) => level switch
    {
        NetCord.Logging.LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
        NetCord.Logging.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
        NetCord.Logging.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
        NetCord.Logging.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
        NetCord.Logging.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        NetCord.Logging.LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
        _ => Microsoft.Extensions.Logging.LogLevel.None
    };
}
