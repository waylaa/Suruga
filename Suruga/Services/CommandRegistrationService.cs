using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Suruga.Options;

namespace Suruga.Services;

/// <summary>
/// Registers slash commands at application startup.
/// </summary>
/// <param name="client">A Discord REST client.</param>
/// <param name="commandService">Service containing the commands to register.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class CommandRegistrationService
(
    RestClient client,
    IApplicationCommandService commandService,
    IOptions<BotOptions> botOptions,
    ILogger<CommandRegistrationService> logger
) : IHostedService
{
    private readonly BotOptions _botOptions = botOptions.Value;
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            CurrentApplication application = await client.GetCurrentApplicationAsync(cancellationToken: cancellationToken);

            IReadOnlyList<ApplicationCommandProperties> rawCommandProperties = await Task
                .WhenAll(commandService.GetCommands().Select(async command => await command.GetRawValueAsync(cancellationToken)));

            ulong? developmentGuildId = _botOptions.DevelopmentGuildId;
            
            if (Program.IsDevelopmentBuild && developmentGuildId is ulong devGuildId)
            {
                LogCommandRegistrationAttempt(devGuildId);

                await client.BulkOverwriteGuildApplicationCommandsAsync
                (
                    applicationId: application.Id,
                    guildId: devGuildId,
                    commands: rawCommandProperties,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await client.BulkOverwriteGlobalApplicationCommandsAsync
                (
                    applicationId: application.Id,
                    commands: rawCommandProperties,
                    cancellationToken: cancellationToken
                );
            }
        
            LogSuccessfulCommandRegistration();
        }
        catch (Exception ex)
        {
            LogException(ex, ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    [LoggerMessage(LogLevel.Information, Message = "Registering slash commands for development guild {GuildId}")]
    private partial void LogCommandRegistrationAttempt(ulong guildId);

    [LoggerMessage(LogLevel.Information, Message = "Guild slash commands registered successfully.")]
    private partial void LogSuccessfulCommandRegistration();

    [LoggerMessage(LogLevel.Error, Message = "{Message}")]
    private partial void LogException(Exception? exception, string message);
}
