using NetCord.Gateway;

namespace Suruga.Handlers;

internal sealed class VoiceChannelOccupancyReader
{
    private readonly GatewayClient _client;
    
    internal VoiceChannelOccupancyReader(GatewayClient client)
        => _client = client;
    
    internal int? GetOtherUsersCount(ulong guildId)
    {
        ulong? channelId = GetCurrentVoiceChannelId(guildId);
        return channelId is null ? null : CountOthers(guildId, channelId.Value);
    }

    internal ulong? GetCurrentVoiceChannelId(ulong guildId)
    {
        if (!_client.Cache.Guilds.TryGetValue(guildId, out Guild? guild))
        {
            return null;
        }

        return guild.VoiceStates.TryGetValue(_client.Id, out VoiceState? selfState) ? selfState.ChannelId : null;
    }

    private int? CountOthers(ulong guildId, ulong channelId)
    {
        if (!_client.Cache.Guilds.TryGetValue(guildId, out Guild? guild))
        {
            return null;
        }

        ulong selfId = _client.Id;
        return guild.VoiceStates.Values.Count(s => s.ChannelId == channelId && s.UserId != selfId);
    }
}
