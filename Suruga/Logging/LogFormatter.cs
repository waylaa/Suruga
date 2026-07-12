using Microsoft.Extensions.Logging;

namespace Suruga.Logging;

/// <summary>
/// Provides helpers for formatting log entries for display.
/// </summary>
internal static class LogFormatter
{
    /// <summary>
    /// Formats log entry components into a display-friendly representation.
    /// </summary>
    /// <param name="logLevel">The severity level of the log entry.</param>
    /// <param name="category">The category associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">The exception associated with the log entry, if any.</param>
    /// <returns>
    /// A tuple containing the formatted log level, formatted category, message, and exception text.
    /// </returns>
    public static (string LogLevel, string Category, string Message, string? ExceptionMessage) Format
	(
		LogLevel logLevel,
		string category,
		string message,
		Exception? exception
	)
	{
		string level = logLevel switch
		{
			LogLevel.Critical    => "Critical",
			LogLevel.Error       => "Error",
			LogLevel.Warning     => "Warning",
			LogLevel.Information => "Info",
			LogLevel.Debug       => "Debug",
			LogLevel.Trace       => "Trace",
			_                    => "None"
		};
		
		return ($"[{level}]", $"[{category}]", message, exception?.ToString());
	}
}
