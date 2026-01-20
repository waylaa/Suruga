using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;
using Suruga.Primitives;

namespace Suruga.Handlers;

/// <summary>
/// Monitors guild voice state changes and automatically cleans up idle audio sessions.
/// </summary>
internal sealed class VoiceStateEventHandler
(
    GatewayClient client,
    BotState botState,
    AudioSessionManager sessionManager,
    ILogger<VoiceStateEventHandler> logger
) : IVoiceStateUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceState arg)
    {
        try
        {
            if (botState.User is null)
            {
                logger.LogDebug("Received voice state update before Ready event.");
                return;
            }

            // Ignore updates from the bot itself.
            if (arg.UserId == botState.User.Id)
            {
                return;
            }

            // If no audio session exists for this guild, do nothing.
            if (!sessionManager.Exists(arg.GuildId))
            {
                return;
            }

            // Try to get the guild from cache.
            if (!client.Cache.Guilds.TryGetValue(arg.GuildId, out Guild? guild))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Guild {Id} is not present in cache.", arg.GuildId);
                }

                return;
            }

            // Get the bot's voice state in this guild.
            if (!guild.VoiceStates.TryGetValue(botState.User.Id, out VoiceState? botVoiceState))
            {
                return;
            }

            if (botVoiceState.ChannelId is not ulong botChannelId)
            {
                return;
            }

            bool isSomeoneElseInVoiceChannel = guild.VoiceStates.Values
                .Any(state => state.ChannelId == botChannelId && state.UserId != botState.User.Id);

            if (!isSomeoneElseInVoiceChannel)
            {
                await sessionManager.RemoveAsync(guild.Id);

                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace
                    (
                        "Auto-stopped session for guild {GuildId}, channel {ChannelId}: no users remain.",
                        guild.Id,
                        botChannelId
                    );
                }
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError
                (
                    ex,
                    "Error handling VoiceStateUpdate for guild {GuildId}, user {UserId}: {Message}",
                    arg.GuildId,
                    arg.UserId,
                    ex.Message
                );
            }
        }
    }
}
