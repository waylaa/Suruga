using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using Suruga.Resolvers;
using Suruga.Transport;
using Suruga.Transport.Factories;

namespace Suruga.Audio;

internal sealed class AudioSessionManager : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly CompositeAudioSourceResolver _resolver;
    private readonly CompositeAudioByteStreamFactory _factory;
    private readonly ConcurrentDictionary<ulong, AudioSession> _sessions = [];

    public AudioSessionManager
    (
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime,
        CompositeAudioSourceResolver sourceResolver,
        CompositeAudioByteStreamFactory factory
    )
    {
        _loggerFactory = loggerFactory;
        _lifetime = lifetime;
        _resolver = sourceResolver;
        _factory = factory;
    }

    internal AudioSession GetOrCreate(GatewayClient client, ulong guildId, ulong channelId)
    {
        return _sessions.GetOrAdd(guildId, new AudioSession
        (
            _loggerFactory,
            _lifetime,
            client,
            _resolver,
            _factory
        ));
    }
    
    internal async Task RemoveAsync(ulong guildId)
    {
        if (_sessions.TryRemove(guildId, out AudioSession? session))
        {
            await session.DisconnectAsync();
        }
    }
    
    internal bool Exists(ulong guildId)
        => _sessions.ContainsKey(guildId);

    public void Dispose()
    {
        foreach (AudioSession session in _sessions.Values)
        {
            session.Dispose();
        }
        
        _sessions.Clear();
    }
}
