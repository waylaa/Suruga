using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Suruga.Commands;

internal sealed class PingCommand : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("ping", "Test the current status of the bot.")]
    public async Task PingAsync()
        => await RespondAsync(InteractionCallback.Pong);
}
