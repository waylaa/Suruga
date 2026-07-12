using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;

namespace Suruga.Handlers;

/// <summary>
/// Handles Discord voice state updates for the bot and keeps the audio connection descriptor
/// in sync with the current voice state.
/// </summary>
/// <remarks>
/// This handler only processes voice state updates for the bot user itself.
/// It updates the underlying audio connection descriptor when the bot joins,
/// moves, or disconnects from a voice channel.
/// </remarks>
internal sealed class VoiceStateUpdateGatewayHandler
(
    GatewayClient client,
    AudioSessionManager sessionManager
) : IVoiceStateUpdateGatewayHandler
{
    /// <summary>
    /// Processes a voice state update event and updates the audio connection descriptor accordingly.
    /// </summary>
    /// <param name="arg">The voice state update payload from Discord.</param>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask HandleAsync(VoiceState arg)
    {
        if (arg.UserId != client.Id || !sessionManager.TryGetSession(arg.GuildId, out AudioSession? session))
        {
            return ValueTask.CompletedTask;
        }

        AudioConnection.AudioConnectionDescriptor descriptor = session.Player.Engine.Connection.Descriptor;

        // Disconnected from voice.
        if (arg.ChannelId is null)
        {
            descriptor.Invalidate();
            return ValueTask.CompletedTask;
        }

        descriptor.UpdateVoiceState(arg.GuildId, arg.UserId, arg.ChannelId, arg.SessionId);
        return ValueTask.CompletedTask;
    }
}
