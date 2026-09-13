using NetCord.Gateway;
using NetCord.Logging;
using Suruga.Audio.Primitives;
using Suruga.Common;
using Suruga.Persistence;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class AudioSession : IAsyncDisposable
{
    internal AudioConnection Connection { get; }
    
    internal AudioPlayer Player { get; }

    internal AudioPlayerMessage PlayerMessage { get; }

    private readonly ulong _guildId;
    
    private bool _isDisposed;

    internal AudioSession
    (
        GatewayClient gatewayClient,
        TrackStreamResolverRouter trackStreamResolverRouter,
        ReadOnlyAudioByteStreamFactory byteStreamFactory,
        TrackQueueRepository repository,
        IVoiceLogger voiceLogger,
        ulong guildId
    )
    {
        _guildId = guildId;
        
        Connection = new AudioConnection(gatewayClient, voiceLogger, guildId);
        Player = new AudioPlayer(trackStreamResolverRouter, byteStreamFactory, repository, Connection.Sink, guildId);
        PlayerMessage = new AudioPlayerMessage();

        Player.PlayerStateChanged += OnPlayerStateChangedAsync;
    }
    
    private Task OnPlayerStateChangedAsync(AudioPlayerState state, Track? track = null, Exception? error = null)
        => PlayerMessage.RefreshAsync(state, track, error);

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        Logger.Debug<AudioSession>($"Disposing audio session in Guild {_guildId}");
        
        Player.PlayerStateChanged -= OnPlayerStateChangedAsync;
        
        await Player.DisposeAsync();
        await Connection.DisposeAsync();
    }
}
