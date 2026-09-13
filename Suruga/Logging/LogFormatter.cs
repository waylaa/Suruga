using Microsoft.Extensions.Logging;

namespace Suruga.Logging;

internal static class LogFormatter
{
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
