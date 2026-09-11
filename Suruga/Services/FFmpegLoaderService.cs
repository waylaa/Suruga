using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Suruga.FFmpeg.Loaders;
using Suruga.FFmpeg.Primitives;
using Suruga.Options;

namespace Suruga.Services;

/// <summary>
/// Initializes FFmpeg by locating native libraries at application startup.
/// </summary>
/// <param name="options">Bot options.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class FFmpegLoaderService(IOptions<BotOptions> options, ILogger<FFmpegLoaderService> logger) : IHostedService
{
    private readonly BotOptions _botOptions = options.Value;
    
    private const string ExpectedVersion = "9.0.1";
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogUsingFFmpegPath(_botOptions.FFmpegPath);
            
            FFmpegLibraryLoader.Initialize(_botOptions.FFmpegPath);
            CheckVersion();
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
    
    private void CheckVersion()
    {
        string versionInfo = FFmpegVersion.Version;

        if (versionInfo.Contains(ExpectedVersion))
        {
            LogSuccessfulLoad();
            return;
        }
        
        ReadOnlySpan<char> span = versionInfo.AsSpan();

        span = span.TrimStart('n');
        int dash = span.IndexOf('-');
        string version = dash >= 0 ? span[..dash].ToString() : span.ToString();
                    
        LogVersionMismatch(version, ExpectedVersion);
    }
    
    [LoggerMessage(LogLevel.Information, Message = "FFmpeg loaded successfully.")]
    private partial void LogSuccessfulLoad();
    
    [LoggerMessage(LogLevel.Warning, Message = "FFmpeg version {actual} does not match the expected version {expected}. Compatibility issues may occur.")]
    private partial void LogVersionMismatch(string actual, string expected);
    
    [LoggerMessage(LogLevel.Information, Message = "Using FFmpeg libraries from '{path}' as configured by 'BOT_FFMPEGPATH'.")]
    private partial void LogUsingFFmpegPath(string path);
}
