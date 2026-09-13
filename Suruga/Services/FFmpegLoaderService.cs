using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Suruga.Common;
using Suruga.FFmpeg.Loaders;
using Suruga.FFmpeg.Primitives;
using Suruga.Options;

namespace Suruga.Services;

internal sealed class FFmpegLoaderService(IOptions<BotOptions> options) : IHostedService
{
    private readonly BotOptions _botOptions = options.Value;
    
    private const string ExpectedVersion = "9.0.1";
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Logger.Trace<FFmpegLoaderService>($"Using FFmpeg libraries from '{_botOptions.FFmpegPath}' as configured by 'BOT_FFMPEGPATH'.");
            
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
    
    private static void CheckVersion()
    {
        string versionInfo = FFmpegVersion.Version;

        if (versionInfo.Contains(ExpectedVersion))
        {
            Logger.Info<FFmpegLoaderService>("FFmpeg loaded successfully.");
            return;
        }
        
        string temp = versionInfo.TrimStart('n');
        int dash = temp.IndexOf('-');
        string version = dash >= 0 ? temp[..dash] : versionInfo;
        
        Logger.Warning<FFmpegLoaderService>($"FFmpeg version {version} does not match the expected version {ExpectedVersion}. Compatibility issues may occur.");
    }
}
