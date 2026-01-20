using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Primitives;

namespace Suruga.Handlers;

internal sealed class ReadyGatewayEventHandler(BotState botState, ILogger<ReadyGatewayEventHandler> logger) : IReadyGatewayHandler
{
    public ValueTask HandleAsync(ReadyEventArgs arg)
    {
        botState.Set(arg.User);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Bot ready as {Username} ({Id})", arg.User.Username, arg.User.Id);
        }

        return ValueTask.CompletedTask;
    }
}
