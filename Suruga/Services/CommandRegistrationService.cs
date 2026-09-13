using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Suruga.Common;
using Suruga.Options;

namespace Suruga.Services;

internal sealed class CommandRegistrationService(RestClient client, IApplicationCommandService commandService, IOptions<BotOptions> botOptions) : IHostedService
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
                Logger.Debug<CommandRegistrationService>($"Registering slash commands for development guild {devGuildId}");

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
        
            Logger.Info<CommandRegistrationService>("Guild slash commands registered successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error<CommandRegistrationService>(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
