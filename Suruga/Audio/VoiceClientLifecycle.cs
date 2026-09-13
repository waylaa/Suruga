using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using Suruga.Audio.Primitives;
using Suruga.Common;

namespace Suruga.Audio;

internal sealed class VoiceClientLifecycle
(
    GatewayClient gatewayClient,
    IVoiceLogger voiceLogger,
    AudioSink sink,
    VoiceHandshakeCoordinator handshake,
    ulong guildId
) : IAsyncDisposable
{
    internal bool IsConnected => _voiceClient is not null;
    
    private readonly SemaphoreSlim _gate = new(1, 1);
    
    private VoiceClient? _voiceClient;
    private Stream? _voiceStream;

    private volatile bool _isDisposed;
    
    internal async Task<CommandResult> ConnectAsync(ulong voiceChannelId)
    {
        await _gate.WaitAsync();

        try
        {
            if (_voiceClient is not null)
            {
                Logger.Trace<VoiceClientLifecycle>($"Audio connection disconnected from bound voice channel {voiceChannelId} in guild {guildId}.");
                return new CommandResult(CommandStatus.AlreadyConnected);
            }

            handshake.Reset();
            await gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, voiceChannelId));

            if (!await handshake.WaitForValidVoiceAsync(TimeSpan.FromSeconds(5)))
            {
                Logger.Warning<VoiceClientLifecycle>($"Voice handshake for guild {guildId} did not complete in time.");
                return new CommandResult(CommandStatus.InvalidVoice);
            }

            await EstablishAsync();
            Logger.Info<VoiceClientLifecycle>($"Voice connection established in guild {guildId}.");

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
            Logger.Info<VoiceClientLifecycle>($"Re-establishing audio connection in guild {guildId}");

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
            await gatewayClient.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));

            handshake.Reset();
            Logger.Info<VoiceClientLifecycle>($"Voice disconnected in guild {guildId}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EstablishAsync()
    {
        VoiceClient voiceClient = new
        (
            handshake.UserId,
            handshake.SessionId,
            handshake.Endpoint!,
            guildId,
            handshake.ChannelId!.Value,
            handshake.Token,
            new VoiceClientConfiguration { Logger = voiceLogger }
        );

        try
        {
            await voiceClient.StartAsync();
            await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));
        }
        catch (Exception ex)
        {
            Logger.Error<VoiceClientLifecycle>(ex, $"Failed to start voice client in guild {guildId}");
            voiceClient.Dispose();
            
            throw;
        }

        Stream voiceStream = voiceClient.CreateVoiceStream();
        await sink.AttachAsync(voiceStream);

        _voiceClient = voiceClient;
        _voiceStream = voiceStream;
    }

    private async Task TeardownAsync()
    {
        if (_voiceStream is not null)
        {
            await sink.DetachAsync();
            await _voiceStream.DisposeAsync();
            
            _voiceStream = null;
        }

        if (_voiceClient is not null)
        {
            if (_voiceClient.Status is not WebSocketStatus.Disconnected)
            {
                await _voiceClient.CloseAsync();
            }

            _voiceClient.Dispose();
            _voiceClient = null;
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        await _gate.WaitAsync();

        try
        {
            await TeardownAsync();
        }
        catch (Exception ex)
        {
            Logger.Error<VoiceClientLifecycle>(ex, $"Error when disposing during voice client teardown in guild {guildId}");
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
