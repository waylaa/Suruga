using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using Suruga.Audio.Primitives;

namespace Suruga.Audio;

internal sealed class VoiceClientLifecycle
{
    internal bool IsConnected => _voiceClient is not null;
    
    private readonly GatewayClient _gatewayClient;
    private readonly IVoiceLogger _voiceLogger;
    private readonly AudioSink _sink;
    private readonly VoiceHandshakeCoordinator _handshake;
    private readonly ulong _guildId;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private VoiceClient? _voiceClient;
    private Stream? _voiceStream;

    internal VoiceClientLifecycle
    (
        GatewayClient gatewayClient,
        IVoiceLogger voiceLogger,
        AudioSink sink,
        VoiceHandshakeCoordinator handshake, ulong guildId
    )
    {
        _gatewayClient = gatewayClient;
        _voiceLogger = voiceLogger;
        _sink = sink;
        _handshake = handshake;
        _guildId = guildId;
    }

    internal async Task<CommandResult> ConnectAsync(ulong voiceChannelId)
    {
        await _gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(_guildId, voiceChannelId));

        if (!await _handshake.WaitForValidVoiceAsync(TimeSpan.FromSeconds(5)))
        {
            return new CommandResult(CommandStatus.InvalidVoice);
        }

        await _gate.WaitAsync();

        try
        {
            if (_voiceClient is not null)
            {
                return new CommandResult(CommandStatus.AlreadyConnected);
            }

            await EstablishAsync();
            return new CommandResult(CommandStatus.Success);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task ReconnectAsync()
    {
        await _gate.WaitAsync();

        try
        {
            await TeardownAsync();
            await EstablishAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task DisconnectAsync()
    {
        await _gate.WaitAsync();

        try
        {
            await TeardownAsync();
            await _gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(_guildId, null));
            
            _handshake.Reset();
        }
        finally
        {
            _gate.Release();
        }
    }
    
    private async Task EstablishAsync()
    {
        // Assumes caller holds _gate.
        VoiceClient voiceClient = new
        (
            _handshake.UserId,
            _handshake.SessionId,
            _handshake.Endpoint!,
            _guildId,
            _handshake.ChannelId!.Value,
            _handshake.Token,
            new VoiceClientConfiguration { Logger = _voiceLogger }
        );

        await voiceClient.StartAsync();
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

        Stream voiceStream = voiceClient.CreateVoiceStream();
        await _sink.AttachAsync(voiceStream);

        _voiceClient = voiceClient;
        _voiceStream = voiceStream;
    }
    
    private async Task TeardownAsync()
    {
        // Assumes caller holds _gate.
        VoiceClient? oldVoiceClient = _voiceClient;
        Stream? oldVoiceStream = _voiceStream;

        _voiceClient = null;
        _voiceStream = null;

        if (oldVoiceStream is not null)
        {
            await _sink.DetachAsync();
            await oldVoiceStream.DisposeAsync();
        }

        if (oldVoiceClient is not null)
        {
            if (oldVoiceClient.Status is not WebSocketStatus.Disconnected)
            {
                await oldVoiceClient.CloseAsync();
            }

            oldVoiceClient.Dispose();
        }
    }
}
