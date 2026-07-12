using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Suruga.Audio;

namespace Suruga.Handlers;

/// <summary>
/// Handles Discord voice server updates and ensures the voice connection is re-established
/// when the guild is moved to a new voice region endpoint.
/// </summary>
/// <remarks>
/// A user or Discord may change the voice server endpoint during region shifts or reconnects.
/// This handler updates the active audio connection descriptor and triggers a reconnect
/// when necessary.
/// </remarks>
internal sealed class VoiceServerUpdateGatewayHandler(AudioSessionManager sessionManager) : IVoiceServerUpdateGatewayHandler
{
    /// <summary>
    /// Processes a voice server update event.
    /// </summary>
    /// <param name="arg">The voice server update event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask HandleAsync(VoiceServerUpdateEventArgs arg)
	{
		if (!sessionManager.TryGetSession(arg.GuildId, out AudioSession? session))
		{
			return;
		}

		AudioConnection connection = session.Player.Engine.Connection;

		if (arg.Endpoint is null)
		{
			connection.Descriptor.Invalidate();
			return;
		}
		
		string? previousEndpoint = connection.Descriptor.Endpoint;
		connection.Descriptor.UpdateVoiceServer(arg.Endpoint, arg.Token);

        // Reconnect only when:
        // - this is not the initial voice connection (previous endpoint exists)
        // - and the endpoint has changed
        if (previousEndpoint is not null && previousEndpoint != arg.Endpoint)
		{
			await connection.ReconnectAsync();
		}
	}
}
