using System.Globalization;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Suruga.Logging;

internal sealed class FileLogProvider : ILoggerProvider
{
	private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);
	
	private readonly LogLevel _minimumLevel;
	private readonly string _logDirectoryPath;
	private readonly bool _isContainer;
	
	private readonly Lock _lock = new();
	private readonly PeriodicTimer? _retentionTimer;
	
	private DateOnly _currentDate;
	private StreamWriter? _logWriter;
	private FileStream? _logStream;
	
	private const string LogFileExtension = ".log";
	private const string CompressedLogFileExtension = ".log.gz";
	private const string DateFormat = "yyyy-MM-dd";
	
    public FileLogProvider(LogLevel minimumLevel, string logDirectoryPath)
	{
		_minimumLevel = minimumLevel;
		_logDirectoryPath = logDirectoryPath;
		_isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

		if (_isContainer)
		{
			return;
		}
		
		Directory.CreateDirectory(_logDirectoryPath);
		_currentDate = DateOnly.FromDateTime(DateTime.UtcNow);

		OpenLogFile(_currentDate);

		_retentionTimer = new PeriodicTimer(TimeSpan.FromHours(1));
		_ = RunRetentionLoopAsync();
	}

	public ILogger CreateLogger(string categoryName)
		=> new FileLogger(this, categoryName, _minimumLevel);

	private async Task RunRetentionLoopAsync()
	{
		try
		{
			ApplyRetentionPolicy();

			while (await _retentionTimer!.WaitForNextTickAsync())
			{
				ApplyRetentionPolicy();
			}
		}
		catch (ObjectDisposedException)
		{
		}
	}
	
    private void WriteLog(LogLevel level, string category, string message, Exception? exception)
	{
		(string logLvl, string cat, string body, string? exceptionMessage) = LogFormatter.Format(level, category, message, exception);

		using (_lock.EnterScope())
		{
			MoveToNextLogFileIfNeeded();
			_logWriter!.WriteLine($"{logLvl} {cat}: {body}");

			if (exceptionMessage is not null)
			{
				_logWriter.WriteLine(exceptionMessage);
			}
		}
	}

    private void OpenLogFile(DateOnly date)
	{
		string path = GetLogFilePath(date);
		
		_logStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
		_logWriter = new StreamWriter(_logStream) { AutoFlush = true };
	}

    private void MoveToNextLogFileIfNeeded()
	{
		DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

		if (today == _currentDate)
		{
			return;
		}

		_logWriter?.Dispose();
		_logStream?.Dispose();

		_currentDate = today;
		OpenLogFile(today);
	}
    
    private void ApplyRetentionPolicy()
	{
		foreach (string filePath in Directory.EnumerateFiles(_logDirectoryPath, "*.log"))
		{
			if (!TryParseDateFromLogFileName(filePath, out DateTime fileDate))
			{
				continue;
			}

			if (DateTime.UtcNow - fileDate > RetentionPeriod)
			{
				TryDeleteFile(filePath);
				continue;
			}

			if (filePath.EndsWith(LogFileExtension, StringComparison.OrdinalIgnoreCase) &&
			    Path.GetFileNameWithoutExtension(filePath) != _currentDate.ToString(DateFormat))
			{
				TryCompressLogFile(filePath);
			}
		}
	}

	private static bool TryParseDateFromLogFileName(string? filePath, out DateTime fileDate)
	{
		string? fileName = Path.GetFileName(filePath)
			?.Replace(CompressedLogFileExtension, string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace(LogFileExtension, string.Empty, StringComparison.OrdinalIgnoreCase);

		if (fileName is null)
		{
			fileDate = default;
			return false;
		}

		return DateTime.TryParseExact(fileName, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out fileDate);
	}

	private static void TryCompressLogFile(string logFilePath)
	{
		string compressedPath = logFilePath + ".gz";

		if (File.Exists(compressedPath))
		{
			return;
		}

		try
		{
			using FileStream outputLogStream = File.Create(compressedPath);
			using GZipStream logCompressionStream = new(outputLogStream, CompressionLevel.Optimal);
			using FileStream inputLogStream = File.OpenRead(logFilePath);

			inputLogStream.CopyTo(logCompressionStream);
		}
		catch
		{
			return;
		}
		
		TryDeleteFile(logFilePath);
	}

	private static void TryDeleteFile(string logFilePath)
	{
		try
		{
			File.Delete(logFilePath);
		}
		catch
		{
			// Ignore.
		}
	}
	
	private string GetLogFilePath(DateOnly date)
		=> Path.Combine(_logDirectoryPath, $"{date.ToString(DateFormat)}.log");
	
	public void Dispose()
	{
		_retentionTimer?.Dispose();
		_logWriter?.Dispose();
		_logStream?.Dispose();
	}

	private sealed class FileLogger : ILogger
	{
		private readonly FileLogProvider _provider;
		private readonly string _categoryName;
		private readonly LogLevel _minimumLevel;
		
        internal FileLogger(FileLogProvider provider, string categoryName, LogLevel minimumLevel)
		{
			_provider = provider;
			_categoryName = categoryName;
			_minimumLevel = minimumLevel;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel) || _provider._isContainer)
			{
				return;
			}
			
			string message = formatter(state, exception);
			_provider.WriteLog(logLevel, _categoryName, message, exception);
		}

		public bool IsEnabled(LogLevel logLevel)
			=> logLevel >= _minimumLevel;

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	}
}
