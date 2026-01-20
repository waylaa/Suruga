using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using Suruga.Audio.Logging;
using Suruga.Resolvers;
using Suruga.Transport;
using Suruga.Transport.Factories;

namespace Suruga.Audio;

internal sealed class AudioSession : IDisposable
{
    internal AudioPlayer Player => _player ?? throw new InvalidOperationException("Call ConnectAsync() first before accessing the player.");
    
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly GatewayClient _gateway;
    private readonly CompositeAudioSourceResolver _resolver;
    private readonly CompositeAudioByteStreamFactory _factory;

    private VoiceClient? _voice;
    private AudioPlayer? _player;
    
    internal AudioSession
    (
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime,
        GatewayClient client,
        CompositeAudioSourceResolver resolver,
        CompositeAudioByteStreamFactory factory
    )
    {
        _loggerFactory = loggerFactory;
        _lifetime = lifetime;
        _gateway = client;
        _resolver = resolver;
        _factory = factory;
    }

    internal async Task ConnectAsync(ulong guildId, ulong channelId)
    {
        if (_voice is not null && _voice.Status is not WebSocketStatus.Disconnected)
        {
            return;
        }
        
        _voice = await _gateway.JoinVoiceChannelAsync
        (
            guildId,
            channelId,
            new VoiceClientConfiguration
            {
                ReceiveHandler = new VoiceReceiveHandler(),
                Logger = new MicrosoftExtensionsLoggerAdapter(_loggerFactory.CreateLogger<AudioSession>())
            }
        );
        
        _player = new AudioPlayer(_loggerFactory, _lifetime, _voice, _resolver, _factory);
    }

    internal async Task DisconnectAsync()
    {
        if (_player is not null)
        {
            await _player.DisposeAsync();
        }
        
        if (_voice is not null && _voice.Status is not WebSocketStatus.Disconnected)
        {
            await _voice.CloseAsync();
        }
    }

    public void Dispose()
        => _voice?.Dispose();
}