using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;
using Suruga.Audio.Primitives;

namespace Suruga.Handlers;

internal sealed class AutoPauseResumeVoiceStateUpdateGatewayHandler(GatewayClient gatewayClient, AudioSessionManager sessionManager) : IVoiceStateUpdateGatewayHandler
{
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
            if (player.State is not AudioPlayerState.Paused)
            {
	            await player.PauseAsync();
            }
		}
		else
		{
            // Auto-resume when unmuted.
            if (player.State is AudioPlayerState.Paused)
            {
	            await player.ResumeAsync();
            }
		}
	}
}
