using NetCord.Gateway;
using NetCord.Logging;
using Suruga.Audio.Primitives;
using Suruga.Common;

namespace Suruga.Audio;

internal sealed class AudioConnection : IAsyncDisposable
{
    internal AudioSink Sink { get; }
    
    private readonly VoiceHandshakeCoordinator _handshake = new();
    private readonly VoiceClientLifecycle _lifecycle;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ulong _guildId;
    
    private ulong _voiceChannelId;
    private string? _lastEndpoint;
    private string? _lastToken;
    
    private bool _isDisposed;

    internal AudioConnection(GatewayClient gatewayClient, IVoiceLogger voiceLogger, ulong guildId)
    {
        _guildId = guildId;

        Sink = new AudioSink();
        _lifecycle = new VoiceClientLifecycle(gatewayClient, voiceLogger, Sink, _handshake, guildId);
    }
    
    internal async Task<CommandResult> ConnectAsync(ulong voiceChannelId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync();

        try
        {
            _voiceChannelId = voiceChannelId;
            return await _lifecycle.ConnectAsync(_voiceChannelId);
        }
        catch (Exception ex)
        {
            Logger.Error<AudioConnection>(ex, $"Failed to connect to voice channel {voiceChannelId} in guild {_guildId}");
        }
        finally
        {
            _gate.Release();
        }
        
        return new CommandResult(CommandStatus.Undefined);
    }

    internal async Task HandleVoiceServerUpdateAsync(string? endpoint, string token)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (endpoint == _lastEndpoint && token == _lastToken)
        {
            return;
        }

        _lastEndpoint = endpoint;
        _lastToken = token;
        
        bool isValid = _handshake.OnVoiceServerUpdate(endpoint, token);

        if (isValid)
        {
            if (!_lifecycle.IsConnected)
            {
                return;
            }
            
            await _gate.WaitAsync();

            try
            {
                await _lifecycle.ReconnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error<AudioConnection>(ex, $"Failed to reconnect voice client during region change in guild {_guildId}");
            }
            finally
            {
                _gate.Release();
            }
        }
        else
        {
            Logger.Warning<AudioConnection>($"Voice server update was invalid. Disconnecting from guild {_guildId}");
            await DisconnectAsync();
        }
    }
    
    internal async Task HandleVoiceStateUpdateAsync(ulong userId, ulong? channelId, string sessionId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        bool isValid = _handshake.OnVoiceStateUpdate(userId, channelId, sessionId);

        if (!isValid)
        {
            Logger.Warning<AudioConnection>($"A user disconnected the bot from voice channel {_voiceChannelId} in guild {_guildId}. Disconnecting the voice client.");
            await DisconnectAsync();
        }
    }
    
    private Task DisconnectAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return DisconnectCoreAsync();
    }

    private async Task DisconnectCoreAsync()
    {
        await _gate.WaitAsync();

        try
        {
            await _lifecycle.DisconnectAsync();
        }
        catch (Exception ex)
        {
            ulong voiceChannelId = Volatile.Read(ref _voiceChannelId);
            Logger.Error<AudioConnection>(ex, $"Failed to disconnect from voice channel {voiceChannelId} in guild {_guildId}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Logger.Debug<AudioConnection>($"Disposing audio connection in guild {_guildId}");
        
        await DisconnectCoreAsync();
        _gate.Dispose();
        await Sink.DisposeAsync();
    }
}
