using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using NetCord.Gateway;
using NetCord.Logging;
using Suruga.Common;
using Suruga.Persistence;
using Suruga.Resolvers;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class AudioSessionManager
(
    GatewayClient gatewayClient,
    TrackStreamResolverRouter resolverRouter,
    ReadOnlyAudioByteStreamFactory byteStreamFactory,
    TrackQueueRepository repository,
    IVoiceLogger voiceLogger
)
{
    private readonly ConcurrentDictionary<ulong, AudioSession> _sessions = [];

    internal AudioSession GetOrCreateSession(ulong guildId)
    {
        Logger.Info<AudioSessionManager>($"Creating/getting audio session for guild {guildId}");
        
        return _sessions.GetOrAdd(guildId, static (id, args) => new AudioSession
        (
            args.gatewayClient,
            args.resolverRouter,
            args.byteStreamFactory,
            args.persistence,
            args.voiceLogger,
            id
        ), (gatewayClient, resolverRouter, byteStreamFactory, persistence: repository, voiceLogger));
    }

    internal bool TryGetSession(ulong guildId, [NotNullWhen(true)] out AudioSession? session)
        => _sessions.TryGetValue(guildId, out session);
    
    internal bool TryRemoveSession(ulong guildId, [NotNullWhen(true)] out AudioSession? session)
        => _sessions.TryRemove(guildId, out session);

    internal async Task DisconnectAllSessionsAsync()
    {
        Logger.Info<AudioSessionManager>($"Disconnecting {_sessions.Count} active audio session(s).");
        
        foreach (AudioSession session in _sessions.Values)
        {
            await session.DisposeAsync();
        }
        
        _sessions.Clear();
    }
}
