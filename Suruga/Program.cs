using System.Net;
using System.Runtime;
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
    internal static bool IsDevelopmentBuild { get; private set; }

    private static async Task Main()
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        Console.Title = "Suruga";
        
        if (RuntimeInformation.RuntimeIdentifier is not ("win-x64" or "linux-x64" or "linux-arm64"))
        {
            await Console.Error.WriteLineAsync($"Unsupported platform: {RuntimeInformation.RuntimeIdentifier}");
            Environment.Exit(1);
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        IsDevelopmentBuild = builder.Environment.IsDevelopment();

        builder.Configuration.AddDotNetEnv();
        ConfigureLogging(builder);
        ConfigureServices(builder);

        IHost host = builder.Build();
        host.AddComponentInteractionModule<AudioTrackInteractionModule>();
        host.AddComponentInteractionModule<HistoryPaginationInteractionModule>();
        host.AddComponentInteractionModule<QueuePaginationInteractionModule>();
        host.AddApplicationCommandModule<AudioCommandsModule>();
        
        await host.RunAsync();
    }

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

        services.Configure<BotOptions>(options =>
        {
            options.Token = config["BOT_TOKEN"]
                ?? throw new InvalidOperationException("Token is missing or invalid.");

            options.DevelopmentGuildId = config.GetValue<ulong?>("BOT_DEVELOPMENTGUILDID");
            options.FFmpegPath = config["BOT_FFMPEGPATH"];
        });

        services.Configure<DatabaseOptions>(options =>
        {
            options.Enable = config.GetValue<bool>("DATABASE_ENABLE");

            options.ConnectionString = config["DATABASE_CONNECTIONSTRING"]
                ?? throw new InvalidOperationException("Database connection string is missing or invalid.");

            options.Name = config["DATABASE_NAME"]
                ?? throw new InvalidOperationException("Database name is missing or invalid.");
        });

        services.Configure<InvidiousCompanionOptions>(options =>
        {
            options.Enable = config.GetValue<bool>("INVIDIOUSCOMPANION_ENABLE");

            options.SecretKey = config["INVIDIOUSCOMPANION_SECRETKEY"]
                ?? throw new InvalidOperationException("Secret key is missing or invalid.");

            options.Host = config["INVIDIOUSCOMPANION_HOST"]
                ?? throw new InvalidOperationException("Host is missing or invalid.");

            options.Port = config.GetValue<ushort>("INVIDIOUSCOMPANION_PORT");
            options.UseHttps = config.GetValue<bool>("INVIDIOUSCOMPANION_USEHTTPS");
        });

        services
            .AddMemoryCache()
            .AddHostedService<FFmpegLoaderService>()
            .AddSingleton(sp => FFmpegLogger.Initialize(sp.GetRequiredService<ILogger<FFmpegLogger>>()))
            .AddSingleton<DatabaseClient>()
            .AddSingleton<TrackQueueStateRepository>()
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
            .AddSingleton<AudioPlayerFactory>()
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
