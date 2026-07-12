using Microsoft.Extensions.Logging;

namespace Suruga.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes log messages to the console using
/// colored output based on the log level.
/// </summary>
/// <remarks>
/// Messages are written in the format:
/// <c>[Level] [Category]: Message</c>,
/// followed by exception details (if present) on a new line.
///
/// <para>
/// This provider does not support logging scopes.
/// </para>
/// </remarks>
/// <param name="minimumLevel">
/// The minimum <see cref="LogLevel"/> that will be written to the console.
/// Messages below this level are ignored.
/// </param>
internal sealed class ConsoleLogProvider(LogLevel minimumLevel) : ILoggerProvider
{
	public ILogger CreateLogger(string categoryName)
		=> new ConsoleLogger(categoryName, minimumLevel);
	
	public void Dispose()
	{
	}

    /// <summary>
    /// Writes colored console output for log messages with a level and category prefix.
    /// </summary>
    /// <remarks>
    /// Output format:
    /// <c>[Level] [Category]: Message</c>
    /// followed by exception details (if any) on a new line.
    /// </remarks>
    private sealed class ConsoleLogger : ILogger
	{
		private static readonly Lock Lock = new();

		private readonly string _categoryName;
		private readonly LogLevel _minimumLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleLogger"/> class.
        /// </summary>
        /// <param name="categoryName">The category name for this logger.</param>
        /// <param name="minimumLevel">The minimum log level to output.</param>
        internal ConsoleLogger(string categoryName, LogLevel minimumLevel)
		{
			_categoryName = categoryName;
			_minimumLevel = minimumLevel;
		}
		
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}
			
			string message = formatter(state, exception);
			
			(string logLvl, string category, string body, string? exceptionMessage) =
				LogFormatter.Format(logLevel, _categoryName, message, exception);

			ConsoleColor foreground = logLevel switch
			{
				LogLevel.Critical    => ConsoleColor.DarkRed,
				LogLevel.Error       => ConsoleColor.Red,
				LogLevel.Warning     => ConsoleColor.DarkYellow,
				LogLevel.Information => ConsoleColor.Green,
				LogLevel.Debug		 => ConsoleColor.DarkGray,
				LogLevel.Trace		 => ConsoleColor.Cyan,
				_					 => ConsoleColor.White
			};

			using (Lock.EnterScope())
			{
				Console.ForegroundColor = foreground;
				Console.Write(logLvl);
				Console.ResetColor();
				
				Console.WriteLine($" {category}: {body}");
				
				if (exceptionMessage is not null)
				{
					Console.WriteLine(exception);
				}
			}
		}

		public bool IsEnabled(LogLevel logLevel)
			=> (int)logLevel >= (int)_minimumLevel;

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	}
}
