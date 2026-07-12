using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using NetCord.Gateway;
using NetCord.Rest;

namespace Suruga.Audio;

internal sealed class AudioSessionManager(AudioPlayerFactory playerFactory, RestClient rest)
{
	private readonly ConcurrentDictionary<ulong, AudioSession> _sessions = [];

	internal AudioSession GetOrCreateSession(GatewayClient client, ulong guildId, ulong textChannelId)
	{
		return _sessions.GetOrAdd(guildId, static (id, args) =>
		{
			AudioPlayer player = args.playerFactory.Create(args.client, id);
			return new AudioSession(player, args.rest, args.textChannelId);
		}, (playerFactory, rest, client, textChannelId));
	}

	internal bool TryGetSession(ulong guildId, [NotNullWhen(true)] out AudioSession? session)
		=> _sessions.TryGetValue(guildId, out session);
	
	internal bool SessionExists(ulong guildId)
		=> _sessions.ContainsKey(guildId);

	internal async ValueTask<bool> TryRemoveSessionAsync(ulong guildId)
	{
		if (!_sessions.TryRemove(guildId, out AudioSession? session))
		{
			return false;
		}

		await session.DisposeAsync();
		return true;
	}

	internal async Task RemoveAllSessionsAsync()
	{
		foreach (AudioSession session in _sessions.Values)
		{
			await session.DisposeAsync();
		}
	}
}
