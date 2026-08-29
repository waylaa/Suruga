using System.Collections.Concurrent;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;

namespace Suruga.Handlers;

/// <summary>
/// Handles voice state updates to automatically disconnect the bot after a period
/// of inactivity in a voice channel.
/// </summary>
/// <remarks>
/// This handler tracks when a voice channel becomes empty (excluding the bot itself)
/// and schedules a delayed disconnect. If users rejoin before the timeout expires,
/// the scheduled disconnect is canceled.
/// </remarks>
internal sealed class InactivityTrackVoiceStateUpdateGatewayHandler
(
	GatewayClient gatewayClient,
	AudioSessionManager sessionManager
) : IVoiceStateUpdateGatewayHandler
{
	private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTimers = [];

    /// <summary>
    /// Processes a voice state update and manages inactivity-based disconnect timers.
    /// </summary>
    /// <param name="arg">The updated voice state information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public ValueTask HandleAsync(VoiceState arg)
	{
		int? connectedUsers = GetOtherUsersInVoiceChannel(arg.GuildId);
		ulong? currentVoiceChannelId = GetCurrentVoiceChannelId(arg.GuildId);

		if (!connectedUsers.HasValue || currentVoiceChannelId is not ulong voiceChannelId)
		{
			return ValueTask.CompletedTask;
		}

        // If users are present, cancel any pending disconnect.
        if (connectedUsers > 0)
		{
			return new ValueTask(CancelDisconnectAsync(voiceChannelId));
		}

        // Otherwise schedule a disconnect if one is not already scheduled.
        CancellationTokenSource cts = new();

		if (!_disconnectTimers.TryAdd(voiceChannelId, cts))
		{
			cts.Dispose();
			return ValueTask.CompletedTask;
		}
		
		_ = DisconnectIfInactive(arg.GuildId, voiceChannelId, cts.Token);
		return ValueTask.CompletedTask;
	}


    /// <summary>
    /// Counts other non-bot users in the bot's current voice channel.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <returns>
    /// The number of other users in the voice channel, or <see langword="null"/> if unavailable.
    /// </returns>
    private int? GetOtherUsersInVoiceChannel(ulong guildId)
	{
		if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out Guild? guild))
		{
			return null;
		}
		
		ulong selfId = gatewayClient.Id;
		IReadOnlyDictionary<ulong, VoiceState> voiceStates = guild.VoiceStates;

		if (!voiceStates.TryGetValue(selfId, out VoiceState? selfVoiceState) || selfVoiceState.ChannelId is not ulong selfVoiceChannelId)
		{
			return null;
		}

		return voiceStates.Values.Count(state => state.ChannelId == selfVoiceChannelId && state.UserId != selfId);
	}


    /// <summary>
    /// Gets the voice channel ID the bot is currently connected to.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <returns>The current voice channel ID, or <see langword="null"/> if not connected.</returns>
    private ulong? GetCurrentVoiceChannelId(ulong guildId)
	{
		if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out Guild? guild))
		{
			return null;
		}

		return guild.VoiceStates.TryGetValue(gatewayClient.Id, out VoiceState? selfVoiceState) ? selfVoiceState.ChannelId : null;
	}


    /// <summary>
    /// Cancels a pending disconnect timer for the specified voice channel.
    /// </summary>
    private async Task CancelDisconnectAsync(ulong voiceChannelId)
	{
		if (_disconnectTimers.TryRemove(voiceChannelId, out CancellationTokenSource? cts))
		{
			await cts.CancelAsync();
		}
	}

    /// <summary>
    /// Waits for inactivity and disconnects the session if no users return.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="voiceChannelId">The voice channel being monitored.</param>
    /// <param name="token">A cancellation token used to cancel the scheduled disconnect.</param>
    private async Task DisconnectIfInactive(ulong guildId, ulong voiceChannelId, CancellationToken token)
	{
		try
		{
			await Task.Delay(TimeSpan.FromMinutes(5), token);
				
			// Refetch voice channel ID and connected users.
			int? connectedUsers = GetOtherUsersInVoiceChannel(guildId);

			if (connectedUsers == 0)
			{
				if (sessionManager.TryRemoveSession(guildId, out AudioSession? session))
				{
					await session.DisposeAsync();
				}
			}
		}
		catch (TaskCanceledException)
		{
			// Ignore.
		}
		finally
		{
			if (_disconnectTimers.TryRemove(voiceChannelId, out CancellationTokenSource? cts))
			{
				cts.Dispose();
			}
		}
	}
}
