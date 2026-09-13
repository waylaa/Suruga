using System.Net;
using System.Runtime.InteropServices;
using DotNetEnv.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Logging;
using Polly;
using Suruga.Audio;
using Suruga.Commands;
using Suruga.Commands.Interactions;
using Suruga.FFmpeg.Logging;
using Suruga.Handlers;
using Suruga.Logging;
using Suruga.Options;
using Suruga.Pagination;
using Suruga.Persistence;
using Suruga.Resolvers;
using Suruga.Resolvers.Local;
using Suruga.Resolvers.Youtube;
using Suruga.Resolvers.Youtube.Clients;
using Suruga.Services;
using Suruga.Transport;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

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

    private static async Task Main()
    {
        Console.Title = "Suruga";
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        ConfigureAppConfiguration(builder);
        ConfigureLogging(builder);
        ConfigureServices(builder);

        IHost host = builder.Build();
        host.AddComponentInteractionModule<TrackInteractionModule>();
        host.AddComponentInteractionModule<HistoryPaginationInteractionModule>();
        host.AddComponentInteractionModule<QueuePaginationInteractionModule>();
        host.AddApplicationCommandModule<AudioCommandsModule>();
        
        await host.RunAsync();
    }

    private static void ConfigureAppConfiguration(HostApplicationBuilder builder)
        => builder.Configuration.AddDotNetEnv();

    private static void ConfigureLogging(HostApplicationBuilder builder)
    {
        bool isDebugLoggingEnabled = IsDevelopmentBuild || builder.Configuration.GetValue<bool>("BOT:ENABLEDEBUGLOGS");
        LogLevel configLevel = isDebugLoggingEnabled ? LogLevel.Debug : LogLevel.Information;

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(configLevel);
        builder.Logging.AddProvider(new ConsoleLogProvider(configLevel));
        builder.Logging.AddProvider(new FileLogProvider(configLevel, Path.Combine(Directory.GetCurrentDirectory(), "logs")));
        builder.Logging.AddFilter((category, level) => category switch
        {
            not null when category.StartsWith("Microsoft.Extensions.Hosting.Internal.Host") => false,
            not null when category.StartsWith("Microsoft.Hosting.Lifetime") => false,
            not null when category.StartsWith("System.Net.Http.HttpClient") && level < LogLevel.Warning => false,
            not null when category.StartsWith("Microsoft.Extensions.Http") && level < LogLevel.Warning => false,
            not null when category.StartsWith("Polly") && level < LogLevel.Warning => false,
            not null when category.StartsWith("NetCord.Hosting.Services.ApplicationCommands.AutocompleteInteractionHandler") => false,
            _ => true
        });
    }
    
    private static void ConfigureServices(HostApplicationBuilder builder)
    {
        IServiceCollection services = builder.Services;
        ConfigurationManager config = builder.Configuration;

        services
            .AddOptions<BotOptions>()
            .Configure(options =>
            {
                options.Token = config.GetValue<string>("BOT_TOKEN") ?? string.Empty;
                options.DevelopmentGuildId = config.GetValue<ulong?>("BOT_DEVELOPMENTGUILDID");
                options.FFmpegPath = IsDevelopmentBuild
                    ? Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native") 
                    : config.GetValue<string>("BOT_FFMPEGPATH") ?? AppContext.BaseDirectory;
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.Token), "Bot token cannot be empty.");

        services.AddOptions<DatabaseOptions>().Configure(options =>
        {
            options.Enable = config.GetValue<bool>("DATABASE_ENABLE");
            options.Path = config.GetValue<string>("DATABASE_PATH");
        });

        services
            .AddOptions<InvidiousCompanionOptions>()
            .Configure(options =>
            {
                options.Enable = config.GetValue<bool>("INVIDIOUSCOMPANION_ENABLE");
                options.SecretKey = config.GetValue<string>("INVIDIOUSCOMPANION_SECRETKEY") ?? string.Empty;
                options.Host = config.GetValue<string>("INVIDIOUSCOMPANION_HOST") ?? string.Empty;
                options.Port = config.GetValue<ushort>("INVIDIOUSCOMPANION_PORT");
                options.UseHttps = config.GetValue<bool>("INVIDIOUSCOMPANION_USEHTTPS");
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "Invidious Companion secret key cannot be empty.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Invidious Companion host cannot be empty.")
            .Validate(options => options.Port is >= 1 and <= 65535, "Invidious Companion port is out of range (Must be between 1-65535).");

        services
            .AddMemoryCache()
            .AddHostedService<LoggerInitializationService>()
            .AddHostedService<FFmpegLoaderService>()
            .AddActivatedSingleton(_ => FFmpegLogger.Initialize(IsDevelopmentBuild))
            .AddSingleton<DatabaseClient>()
            .AddSingleton<SqliteOperationExecutor>()
            .AddSingleton<TrackQueueRepository>()
            .AddHttpClient("youtube-bytestream").AddStandardResilienceHandler(CreateHttpResiliencePipeline).Services
            .AddHttpClient<InvidiousCompanionClient>().AddStandardResilienceHandler(CreateHttpResiliencePipeline).Services
            .AddHttpClient<YoutubeAndroidClient>().AddStandardResilienceHandler(CreateHttpResiliencePipeline).Services
            .AddHttpClient<YoutubeAndroidVrClient>().AddStandardResilienceHandler(CreateHttpResiliencePipeline).Services
            .AddSingleton<YoutubeClient, YoutubeAndroidVrClient>()
            .AddSingleton<YoutubeClient, YoutubeAndroidClient>()
            .AddSingleton<YoutubeClientRouter>()
            .AddSingleton<ITrackResolver, YoutubeTrackResolver>()
            .AddSingleton<ITrackResolver, LocalTrackResolver>()
            .AddSingleton<TrackResolverRouter>()
            .AddSingleton<ITrackStreamResolver, LocalTrackStreamResolver>()
            .AddSingleton<ITrackStreamResolver, YoutubeTrackStreamResolver>()
            .AddSingleton<TrackStreamResolverRouter>()
            .AddSingleton<ReadOnlyAudioByteStreamFactory>()
            .AddSingleton<AudioSessionManager>()
            .AddSingleton<PaginatorManager>()
            .AddSingleton<IVoiceLogger, AudioLogger>()
            .AddDiscordGateway((options, sp) =>
            {
                BotOptions botOptions = sp.GetRequiredService<IOptions<BotOptions>>().Value;

                options.Token = botOptions.Token;
                options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates;
            })
            .AddApplicationCommands(o => o.AutoRegisterCommands = false)
            .AddComponentInteractions()
            .AddGatewayHandler<VoiceStateUpdateGatewayHandler>()
            .AddGatewayHandler<VoiceServerUpdateGatewayHandler>()
            .AddGatewayHandler<DisconnectGatewayHandler>()
            .AddGatewayHandler<AutoPauseResumeVoiceStateUpdateGatewayHandler>()
            .AddGatewayHandler<InactivityTrackVoiceStateUpdateGatewayHandler>()
            .AddHostedService<CommandRegistrationService>();
    }

    private static void CreateHttpResiliencePipeline(HttpStandardResilienceOptions options)
    {
        options.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(response => response.StatusCode is not HttpStatusCode.Forbidden && // Don't retry 403.
                                      (int)response.StatusCode >= 500) // Only retry server errors.
            .Handle<HttpRequestException>(ex => ex.StatusCode != HttpStatusCode.Forbidden);

        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;

        options.CircuitBreaker.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(response => response.StatusCode is not HttpStatusCode.Forbidden && // Don't circuit break on 403.
                                      (int)response.StatusCode >= 500);
    }
}
