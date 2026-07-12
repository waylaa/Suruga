using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;

namespace Suruga.Handlers;

internal sealed class DisconnectGatewayHandler(AudioSessionManager sessionManager) : IDisconnectGatewayHandler
{
    public async ValueTask HandleAsync(DisconnectEventArgs arg)
    {
        if (arg.Reconnect)
        {
            return;
        }

        await sessionManager.RemoveAllSessionsAsync();
    }
}
