using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Suruga.Logging;
using Suruga.Options;

namespace Suruga.Services;

/// <summary>
/// Initializes FFmpeg by locating native libraries at application startup.
/// </summary>
/// <param name="services">DI services.</param>
/// <param name="options">Bot options.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class FFmpegLoaderService
(
    IServiceProvider services,
    IOptions<BotOptions> options,
    ILogger<FFmpegLoaderService> logger
) : IHostedService
{
    private readonly BotOptions _botOptions = options.Value;
    
    private const string ExpectedVersion = "8.1";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetLocation(out string path))
            {
                throw new InvalidOperationException("Failed to find FFmpeg.");
            }
        
            ffmpeg.RootPath = path;
        
            CheckVersion();
            LogSuccessfulLoad();
        
            _ = services.GetRequiredService<FFmpegLogger>(); // Resolve this logger so that it actually works.
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private bool TryGetLocation(out string path)
    {
        // Check for BotOptions.FFmpegPath from .env
        if (!string.IsNullOrWhiteSpace(_botOptions.FFmpegPath))
        {
            path = _botOptions.FFmpegPath;
            return true;
        }
        
        Dictionary<string, string[]> map = FunctionResolverBase.LibraryDependenciesMap;

        // Check for ffmpeg runtimes alongside this executable.
        string extension = OperatingSystem.IsWindows() ? "dll" : "so";
        string bundledDirectoryPath = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native");
        
        bool allBundledFound = map.Keys.All(dep => OperatingSystem.IsWindows()
            ? Directory.GetFiles(bundledDirectoryPath, $"{dep}-*.{extension}").Length > 0
            : Directory.GetFiles(bundledDirectoryPath, $"lib{dep}.so*").Length > 0);

        if (allBundledFound)
        {
            path = bundledDirectoryPath;
            return true;
        }

        // Check for windows or linux system-wide installations.
        if (OperatingSystem.IsWindows())
        {
            string? globalPath = Environment.GetEnvironmentVariable("Path")
                ?.Split(';')
                .FirstOrDefault(x => x.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(globalPath) && Directory.Exists(globalPath))
            {
                path = globalPath;
                return true;
            }
        }

        if (OperatingSystem.IsLinux())
        {
            string[] linuxLocations =
            [
                "/usr/lib/",
                "/usr/lib/x86_64-linux-gnu/"
            ];

            foreach (string linuxLocation in linuxLocations)
            {
                if (!Directory.Exists(linuxLocation))
                {
                    continue;
                }
                
                IEnumerable<string?> soFiles = Directory
                    .EnumerateFiles(linuxLocation, "*.so*")
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Select(fileName =>
                    {
                        ReadOnlySpan<char> span = fileName.AsSpan();
                        
                        if (span.StartsWith("lib"))
                        {
                            span = span[3..];
                        }

                        int extensionIndex = span.IndexOf(".so", StringComparison.Ordinal);
                        
                        return extensionIndex >= 0
                            ? span[..extensionIndex].ToString()
                            : span.ToString();
                    });
                
                if (map.Keys.All(soFiles.Contains))
                {
                    path = linuxLocation;
                    return true;
                }
            }
        }

        path = string.Empty;
        return false;
    }

    private void CheckVersion()
    {
        string versionInfo = ffmpeg.av_version_info();

        if (versionInfo.Contains(ExpectedVersion))
        {
            return;
        }
        
        ReadOnlySpan<char> span = versionInfo.AsSpan();

        span = span.TrimStart('n');
        int dash = span.IndexOf('-');

        string version = dash >= 0
            ? span[..dash].ToString()
            : span.ToString();
                    
        LogVersionMismatch(version, ExpectedVersion);
    }
    
    [LoggerMessage(LogLevel.Information, Message = "FFmpeg loaded successfully.")]
    private partial void LogSuccessfulLoad();
    
    [LoggerMessage(LogLevel.Warning, Message = "FFmpeg version {actual} does not match the expected version {expected}. Compatibility issues may occur.")]
    private partial void LogVersionMismatch(string actual, string expected);
}
