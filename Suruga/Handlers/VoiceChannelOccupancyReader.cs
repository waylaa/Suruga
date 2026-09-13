using NetCord.Gateway;

namespace Suruga.Handlers;

internal sealed class VoiceChannelOccupancyReader(GatewayClient client)
{
    internal int? GetOtherUsersCount(ulong guildId)
    {
        ulong? channelId = GetCurrentVoiceChannelId(guildId);
        return channelId is null ? null : CountOthers(guildId, channelId.Value);
    }

    internal ulong? GetCurrentVoiceChannelId(ulong guildId)
    {
        if (!client.Cache.Guilds.TryGetValue(guildId, out Guild? guild))
        {
            return null;
        }

        return guild.VoiceStates.TryGetValue(client.Id, out VoiceState? selfState) ? selfState.ChannelId : null;
    }

    private int? CountOthers(ulong guildId, ulong channelId)
    {
        if (!client.Cache.Guilds.TryGetValue(guildId, out Guild? guild))
        {
            return null;
        }

        ulong selfId = client.Id;
        return guild.VoiceStates.Values.Count(s => s.ChannelId == channelId && s.UserId != selfId);
    }
}
