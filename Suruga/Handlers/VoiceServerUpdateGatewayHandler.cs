using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;

namespace Suruga.Handlers;

internal sealed class VoiceServerUpdateGatewayHandler(AudioSessionManager sessionManager) : IVoiceServerUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceServerUpdateEventArgs arg)
	{
		if (!sessionManager.TryGetSession(arg.GuildId, out AudioSession? session))
		{
			return;
		}
		
		await session.Connection.HandleVoiceServerUpdateAsync(arg.Endpoint, arg.Token);
	}
}
