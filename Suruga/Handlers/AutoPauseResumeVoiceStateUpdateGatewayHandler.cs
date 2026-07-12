using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;
using Suruga.Audio.Primitives;

namespace Suruga.Handlers;

/// <summary>
/// Handles voice state updates and automatically pauses or resumes playback when the bot
/// is server muted or unmuted.
/// </summary>
/// <remarks>
/// This handler reacts only to voice state changes for the bot itself.
/// </remarks>
internal sealed class AutoPauseResumeVoiceStateUpdateGatewayHandler
(
	GatewayClient gatewayClient,
	AudioSessionManager sessionManager
) : IVoiceStateUpdateGatewayHandler
{
    /// <summary>
    /// Processes a voice state update event.
    /// </summary>
    /// <param name="arg">The updated voice state information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask HandleAsync(VoiceState arg)
	{
        // Ignore voice state updates that are not from this bot.
        if (arg.UserId != gatewayClient.Id)
		{
			return;
		}

		if (!sessionManager.TryGetSession(arg.GuildId, out AudioSession? session))
		{
			return;
		}
		
		AudioPlayer player = session.Player;

        if (arg.IsMuted)
		{
            // Auto-pause when server-muted.
            if (player.State is not AudioPlaybackState.Paused)
			{
				await player.PauseAsync();
			}
		}
		else
		{
            // Auto-resume when unmuted.
            if (player.State is AudioPlaybackState.Paused)
			{
				await player.ResumeAsync();
			}
		}
	}
}
