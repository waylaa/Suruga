using System.Runtime.InteropServices;
using System.Threading.RateLimiting;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Suruga.Audio;
using Suruga.Audio.FFmpeg.Logging;
using Suruga.Commands;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Resolvers.Abstractions;
using Suruga.Resolvers.Local;
using Suruga.Resolvers.Youtube;
using Suruga.Resolvers.Youtube.Clients;
using Suruga.Resolvers.Youtube.Clients.Abstractions;
using Suruga.Services;
using Suruga.Transport.Abstractions;
using Suruga.Transport.Factories;

namespace Suruga;

internal sealed class Program
{
    internal static bool IsDevelopmentBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    internal static async Task Main(string[] args)
    {
        Console.Title = "Suruga";

        ffmpeg.RootPath = Path.Combine
        (
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64",
            "native"
        );

        // Remove "postproc" dependency. (https://github.com/Ruslan-B/FFmpeg.AutoGen/issues/334)
        Dictionary<string, string[]> filtered = FunctionResolverBase.LibraryDependenciesMap
            .Where(kvp => !string.Equals(kvp.Key, "postproc", StringComparison.OrdinalIgnoreCase))
            .ToDictionary
            (
                kvp => kvp.Key,
                kvp => kvp.Value.Where(d => !string.Equals(d, "postproc", StringComparison.OrdinalIgnoreCase)).ToArray()
            );

        FunctionResolverBase.LibraryDependenciesMap.Clear();

        foreach (KeyValuePair<string, string[]> kvp in filtered)
        {
            FunctionResolverBase.LibraryDependenciesMap[kvp.Key] = kvp.Value;
        }

        IHost host = Host
            .CreateDefaultBuilder()
            .UseConsoleLifetime(x => x.SuppressStatusMessages = true)
            .ConfigureAppConfiguration(builder => builder.AddCommandLine(args).AddJsonFile("settings.json").Build())
            .ConfigureServices(services =>
            {
                services
                .AddHttpClient("client")
                .AddResilienceHandler("yt_web_resilience_pipeline", YoutubeClientResiliencePipeline);

                services.AddHttpClient("apkmirror");

                services
                .AddLogging(builder => builder.AddConsole().AddFFmpeg().SetMinimumLevel(IsDevelopmentBuild ? LogLevel.Debug : LogLevel.Information))
                .AddMemoryCache()
                .AddSingleton<YoutubeClientBase, YoutubeAndroidClient>()
                .AddSingleton<YoutubeClientBase, YoutubeMusicAndroidClient>()
                .AddSingleton<YoutubeClientBase, YoutubeEmbeddedAndroidClient>()
                .AddSingleton(x => new CompositeYoutubeClient(x.GetServices<YoutubeClientBase>()))
                .AddSingleton<IAudioSourceResolver, LocalAudioSourceResolver>()
                .AddSingleton<IAudioSourceResolver, YoutubeAudioSourceResolver>()
                .AddSingleton(sp => new CompositeAudioSourceResolver(sp.GetServices<IAudioSourceResolver>()))
                .AddSingleton<IAudioByteStreamFactory, LocalAudioByteStreamFactory>()
                .AddSingleton<IAudioByteStreamFactory, YoutubeAudioByteStreamFactory>()
                .AddSingleton(sp => new CompositeAudioByteStreamFactory(sp.GetServices<IAudioByteStreamFactory>()))
                .AddSingleton<AudioSessionManager>()
                .AddSingleton<BotState>()
                .AddGatewayHandlers(typeof(Program).Assembly)
                .AddApplicationCommands(x => x.AutoRegisterCommands = false)
                .AddHostedService<SlashCommandRegistrationService>()
                .AddDiscordGateway((options, provider) =>
                {
                    IConfiguration configuration = provider.GetRequiredService<IConfiguration>();

                    options.Token = configuration["token"];
                    options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates;
                });
            })
            .Build();

        host.AddApplicationCommandModule<AudioCommands>();
        host.AddApplicationCommandModule<PingCommand>();

        await host.RunAsync();
    }

    private static void YoutubeClientResiliencePipeline(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder
        .AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(3)
        })
        .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            MaxDelay = TimeSpan.FromMilliseconds(200),
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>() // Timeouts, cancellations.
                .HandleResult(message => !message.IsSuccessStatusCode || (int)message.StatusCode is >= 500 and < 600)
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 0.7, // 70% failures over sampling duration.
            MinimumThroughput = 10, // Minimum requests to evaluate.
            BreakDuration = TimeSpan.FromSeconds(20),
            SamplingDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(message => !message.IsSuccessStatusCode || (int)message.StatusCode >= 500)
        })
        .AddRateLimiter(new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3, // Max 3 calls per window.
            Window = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 20
        }));
    }
}
