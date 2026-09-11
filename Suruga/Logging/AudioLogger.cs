using Microsoft.Extensions.Logging;
using NetCordLogLevel = NetCord.Logging.LogLevel;

namespace Suruga.Logging;

/// <summary>
/// Adapts NetCord voice logging to <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// This logger implements <see cref="NetCord.Logging.IVoiceLogger"/> and forwards all log
/// entries to <see cref="ILogger{TCategoryName}"/>.
///
/// <para>
/// Log levels from NetCord are mapped to their closest equivalent <see cref="LogLevel"/> values.
/// </para>
/// </remarks>
/// <param name="logger">
/// The underlying Microsoft logger used to receive forwarded log entries.
/// </param>
internal sealed partial class AudioLogger(ILogger<AudioLogger> logger) : NetCord.Logging.IVoiceLogger
{
    /// <summary>
    /// Determines whether logging is enabled for the specified NetCord log level.
    /// </summary>
    /// <param name="logLevel">The NetCord log level to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> if logging is enabled for the mapped <see cref="LogLevel"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsEnabled(NetCordLogLevel logLevel)
        => logger.IsEnabled(Map(logLevel));

    /// <summary>
    /// Writes a log entry from NetCord to the underlying Microsoft logging system.
    /// </summary>
    /// <typeparam name="TState">The type of the state object.</typeparam>
    /// <param name="logLevel">The NetCord log level of the entry.</param>
    /// <param name="state">The state object associated with the log entry.</param>
    /// <param name="exception">An exception associated with the log entry, if any.</param>
    /// <param name="formatter">Formats the state and exception into a log message string.</param>
    public void Log<TState>(NetCordLogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Log(Map(logLevel), exception, formatter(state, exception));

    /// <summary>
    /// Maps a NetCord log level to a Microsoft <see cref="LogLevel"/>.
    /// </summary>
    /// <param name="level">The NetCord log level to map.</param>
    /// <returns>
    /// The equivalent <see cref="LogLevel"/>, or <see cref="LogLevel.None"/> if unmapped.
    /// </returns>
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
    
    [LoggerMessage(Message = "{Message}")]
    private partial void Log(LogLevel level, Exception? exception, string message);
}
