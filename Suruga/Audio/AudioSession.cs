using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Logging;
using Suruga.Audio.Primitives;
using Suruga.Persistence;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class AudioSession : IAsyncDisposable
{
    internal AudioConnection Connection { get; }
    
    internal AudioPlayer Player { get; }

    internal AudioPlayerMessageHandler PlayerMessage { get; }

    private bool _isDisposed;

    internal AudioSession
    (
        GatewayClient gatewayClient,
        TrackStreamResolverRouter trackStreamResolverRouter,
        ReadOnlyAudioByteStreamFactory byteStreamFactory,
        TrackQueueStateRepository repository,
        IVoiceLogger voiceLogger,
        ILoggerFactory loggerFactory,
        ulong guildId
    )
    {
        Connection = new AudioConnection(gatewayClient, voiceLogger, guildId);
        Player = new AudioPlayer(trackStreamResolverRouter, byteStreamFactory, repository, Connection.Sink, loggerFactory, guildId);
        PlayerMessage = new AudioPlayerMessageHandler();

        Connection.Disconnected += OnDisconnectedAsync;
        Player.PlayerStateChanged += OnPlayerStateChangedAsync;
    }

    private ValueTask OnDisconnectedAsync()
        => Player.DisposeAsync();
    
    private Task OnPlayerStateChangedAsync(AudioPlayerState state, Track? track, Exception? error)
        => PlayerMessage.UpdateAsync(state, track, error);

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        Player.PlayerStateChanged -= OnPlayerStateChangedAsync;
        await Connection.DisposeAsync();
        await Player.DisposeAsync();
    }
}
