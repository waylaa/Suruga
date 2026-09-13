using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;

namespace Suruga.Handlers;

internal sealed class InactivityTrackVoiceStateUpdateGatewayHandler(GatewayClient gatewayClient, AudioSessionManager sessionManager) : IVoiceStateUpdateGatewayHandler
{
	private readonly VoiceChannelOccupancyReader _occupancy = new(gatewayClient);
	private readonly InactivityDisconnectScheduler _scheduler = new(TimeSpan.FromMinutes(5));

	public async ValueTask HandleAsync(VoiceState arg)
	{
		int? connectedUsers = _occupancy.GetOtherUsersCount(arg.GuildId);
		ulong? voiceChannelId = _occupancy.GetCurrentVoiceChannelId(arg.GuildId);

		if (connectedUsers is null || voiceChannelId is not ulong channelId)
		{
			return;
		}

		if (connectedUsers > 0)
		{
			await _scheduler.CancelAsync(channelId);
			return;
		}

		_scheduler.TrySchedule(channelId, async _ =>
		{
			if (_occupancy.GetOtherUsersCount(arg.GuildId) == 0 && sessionManager.TryRemoveSession(arg.GuildId, out AudioSession? session))
			{
				await session.DisposeAsync();
			}
		});
	}
}
