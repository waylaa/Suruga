using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;

namespace Suruga.Handlers;

internal sealed class VoiceStateUpdateGatewayHandler (GatewayClient client, AudioSessionManager sessionManager ) : IVoiceStateUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceState arg)
    {
        if (arg.UserId != client.Id || !sessionManager.TryGetSession(arg.GuildId, out AudioSession? session))
        {
            return;
        }

        await session.Connection.HandleVoiceStateUpdateAsync(arg.UserId, arg.ChannelId, arg.SessionId);
    }
}
