using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Suruga.Services;

internal sealed class SlashCommandRegistrationService
(
    RestClient restClient,
    IApplicationCommandService commandService,
    ILogger<SlashCommandRegistrationService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CurrentApplication application = await restClient.GetCurrentApplicationAsync(cancellationToken: stoppingToken);

        IReadOnlyList<ApplicationCommandProperties> rawCommandProperties = await Task
            .WhenAll(commandService.GetCommands().Select(async command => await command.GetRawValueAsync(stoppingToken)));

        if (Program.IsDevelopmentBuild)
        {
            const ulong developmentGuildId = 1270722727627587584;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Registering slash commands for development guild {GuildId}", developmentGuildId);
            }

            await restClient.BulkOverwriteGuildApplicationCommandsAsync
            (
                applicationId: application.Id,
                guildId: developmentGuildId,
                commands: rawCommandProperties,
                cancellationToken: stoppingToken
            );

            logger.LogInformation("Guild slash commands registered successfully.");
        }
        else
        {
            await restClient.BulkOverwriteGlobalApplicationCommandsAsync
            (
                applicationId: application.Id,
                commands: rawCommandProperties,
                cancellationToken: stoppingToken
            );
        }
    }
}
