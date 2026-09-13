using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Suruga.Common;

public static partial class Logger
{
    private static readonly ConcurrentDictionary<Type, ILogger> Loggers = [];
    
    private static ILoggerFactory? _factory;

    public static void Initialize(ILoggerFactory factory)
    {
        if (Interlocked.CompareExchange(ref _factory, factory, null) is not null)
        {
            throw new InvalidOperationException("Global logging has already been initialized.");
        }
    }

    public static void Log<T>(LogLevel level, string message, Exception? exception = null)
    {
        ILogger<T> logger = Get<T>();

        if (logger.IsEnabled(level))
        {
            Log(logger, level, message, exception);
        }
    }

    public static void Trace<T>(string message)
        => LogTrace(Get<T>(), message);
    
    public static void Debug<T>(string message)
        => LogDebug(Get<T>(), message);
    
    public static void Info<T>(string message)
        => LogInfo(Get<T>(), message);
    
    public static void Warning<T>(string message)
        => LogWarning(Get<T>(), message);
    
    public static void Warning<T>(Exception exception)
        => LogWarning(Get<T>(), exception);
    
    public static void Warning<T>(Exception exception, string message)
        => LogWarning(Get<T>(), exception, message);
    
    public static void Error<T>(string message)
        => LogError(Get<T>(), message);
    
    public static void Error<T>(Exception exception)
        => LogError(Get<T>(), exception);
    
    public static void Error<T>(Exception exception, string message)
        => LogError(Get<T>(), exception, message);

    public static void Shutdown()
    {
        ILoggerFactory? factory = Interlocked.Exchange(ref _factory, null);
        
        Loggers.Clear();
        factory?.Dispose();
    }
    
    private static ILogger<T> Get<T>()
    {
        ILoggerFactory factory = Volatile.Read(ref _factory) ?? throw new InvalidOperationException("Global logging has not been initialized.");

        return (ILogger<T>)Loggers.GetOrAdd
        (
            typeof(T),
            static (_, factory) => factory.CreateLogger<T>(),
            factory
        );
    }

    [LoggerMessage(Message = "{Message}")]
    private static partial void Log(ILogger logger, LogLevel level, string message, Exception? exception = null);
    
    [LoggerMessage(LogLevel.Trace, Message = "{Message}")]
    private static partial void LogTrace(ILogger logger, string message);
    
    [LoggerMessage(LogLevel.Debug, Message = "{Message}")]
    private static partial void LogDebug(ILogger logger, string message);
    
    [LoggerMessage(LogLevel.Information, Message = "{Message}")]
    private static partial void LogInfo(ILogger logger, string message);
    
    [LoggerMessage(LogLevel.Warning, Message = "{Message}")]
    private static partial void LogWarning(ILogger logger, string message);
    
    [LoggerMessage(LogLevel.Warning)]
    private static partial void LogWarning(ILogger logger, Exception exception);
    
    [LoggerMessage(LogLevel.Warning, Message = "{Message}")]
    private static partial void LogWarning(ILogger logger, Exception exception, string message);

    [LoggerMessage(LogLevel.Error, Message = "{Message}")]
    private static partial void LogError(ILogger logger, string message);
    
    [LoggerMessage(LogLevel.Error)]
    private static partial void LogError(ILogger logger, Exception exception);
    
    [LoggerMessage(LogLevel.Error, Message = "{Message}")]
    private static partial void LogError(ILogger logger, Exception exception, string message);
}
