using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Logging;
using Suruga.Persistence;
using Suruga.Resolvers;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class AudioSessionManager
(
    GatewayClient gatewayClient,
    TrackStreamResolverRouter trackStreamResolverRouter,
    ReadOnlyAudioByteStreamFactory byteStreamFactory,
    TrackQueueStateRepository repository,
    IVoiceLogger voiceLogger,
    ILoggerFactory loggerFactory
)
{
    private readonly ConcurrentDictionary<ulong, AudioSession> _sessions = [];

    internal AudioSession GetOrCreateSession(ulong guildId, ulong textChannelId)
    {
        if (_sessions.TryGetValue(guildId, out AudioSession? session))
        {
            return session;
        }

        session = new AudioSession
        (
            gatewayClient,
            trackStreamResolverRouter,
            byteStreamFactory,
            repository,
            voiceLogger,
            loggerFactory,
            guildId
        );
        
        _sessions[guildId] = session;
        return session;
    }

    internal bool TryGetSession(ulong guildId, [NotNullWhen(true)] out AudioSession? session)
        => _sessions.TryGetValue(guildId, out session);
    
    internal bool TryRemoveSession(ulong guildId, [NotNullWhen(true)] out AudioSession? session)
        => _sessions.TryRemove(guildId, out session);

    internal async Task DisconnectAllSessionsAsync()
    {
        foreach (AudioSession session in _sessions.Values)
        {
            await session.DisposeAsync();
        }
    }
}
