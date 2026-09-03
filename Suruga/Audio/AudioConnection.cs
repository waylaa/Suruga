using System.Threading.Channels;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using Suruga.Audio.Commands;
using Suruga.Audio.Commands.Connection;
using Suruga.Audio.Commands.Voice;
using Suruga.Audio.Primitives;

namespace Suruga.Audio;

internal sealed class AudioConnection : IAsyncDisposable
{
    internal event Func<ValueTask>? Disconnected;
    
    internal AudioSink Sink { get; }

    private readonly GatewayClient _gatewayClient;
    private readonly IVoiceLogger _voiceLogger;
    private readonly ulong _guildId;
    
    private readonly Channel<VoiceEventCommand> _voiceEvents = Channel.CreateUnbounded<VoiceEventCommand>();
    private readonly Channel<ConnectionCommand> _commands = Channel.CreateUnbounded<ConnectionCommand>();
    private readonly Task _voiceEventLoopTask;
    private readonly Task _commandLoopTask;
    
    private readonly SemaphoreSlim _gate = new(1, 1);
    
    private TaskCompletionSource _voiceServerTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _voiceStateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    private VoiceClient? _voiceClient;
    private Stream? _voiceStream;

    private string? _endpoint;
    private string _token = string.Empty;
    private ulong _userId;
    private ulong? _channelId;
    private string _sessionId = string.Empty;

    private bool _isDisposed;
    
    public AudioConnection(GatewayClient gatewayClient, IVoiceLogger voiceLogger, ulong guildId)
    {
        _gatewayClient = gatewayClient;
        _voiceLogger = voiceLogger;
        _guildId = guildId;
        
        _voiceEventLoopTask = HandleVoiceEventsAsync();
        _commandLoopTask = HandleCommandsAsync();
        
        Sink = new AudioSink();
    }

    internal async ValueTask<CommandResult> PostAsync(AudioCommand command, CancellationToken token = default)
    {
        switch (command)
        {
            case ConnectionCommand connection:
                await _commands.Writer.WriteAsync(connection, token);
                break;
            
            case VoiceEventCommand voiceEvent:
                await _voiceEvents.Writer.WriteAsync(voiceEvent, token);
                break;
            
            default: return new CommandResult(CommandStatus.Undefined);
        }
        
        await using (token.Register(() => command.SetCanceled(token)))
        {
            return await command.Task;
        }
    }

    private async Task HandleVoiceEventsAsync()
    {
        await foreach (VoiceEventCommand command in _voiceEvents.Reader.ReadAllAsync())
        {
            try
            {
                CommandResult result = command switch
                {
                    VoiceServerUpdateEventCommand voiceServer => await VoiceServerUpdate(voiceServer),
                    VoiceStateUpdateEventCommand voiceState => await VoiceStateUpdateAsync(voiceState),
                    _ => new CommandResult(CommandStatus.Undefined)
                };

                command.SetResult(result);
            }
            catch (Exception ex)
            {
                command.SetException(ex);
            }
        }
    }

    private async Task HandleCommandsAsync()
    {
        await foreach (ConnectionCommand command in _commands.Reader.ReadAllAsync())
        {
            try
            {
                CommandResult result = command switch
                {
                    ConnectCommand connect => await ConnectAsync(connect),
                    DisconnectCommand => await DisconnectAsync(),
                    _ => new CommandResult(CommandStatus.Undefined)
                };

                command.SetResult(result);
            }
            catch (Exception ex)
            {
                command.SetException(ex);
            }
        }
    }

    private async Task<CommandResult> ConnectAsync(ConnectCommand command)
    {
        if (_isDisposed)
        {
            return new CommandResult(CommandStatus.Undefined);
        }

        if (_voiceClient is not null)
        {
            return new CommandResult(CommandStatus.AlreadyConnected);
        }

        await _gatewayClient.UpdateVoiceStateAsync(
            new VoiceStateProperties(_guildId, command.VoiceChannelId));
        
        bool result = await WaitForValidVoiceAsync(TimeSpan.FromSeconds(5));

        if (!result)
        {
            return new CommandResult(CommandStatus.InvalidVoice);
        }

        await _gate.WaitAsync();

        try
        {
            if (_isDisposed)
            {
                return new CommandResult(CommandStatus.Undefined);
            }

            if (_voiceClient is not null)
            {
                return new CommandResult(CommandStatus.AlreadyConnected);
            }

            _voiceClient = new VoiceClient
            (
                _userId,
                _sessionId,
                _endpoint!,
                _guildId,
                _channelId!.Value,
                _token,
                new VoiceClientConfiguration { Logger = _voiceLogger }
            );

            await _voiceClient.StartAsync();
            await _voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

            _voiceStream = _voiceClient.CreateVoiceStream();
            await Sink.AttachAsync(_voiceStream);

            return new CommandResult(CommandStatus.Success);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CommandResult> DisconnectAsync()
    {
        await DisconnectCoreAsync();
        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> VoiceServerUpdate(VoiceServerUpdateEventCommand eventCommand)
    {
        _endpoint = eventCommand.Endpoint;
        _token = eventCommand.Token;

        if (_endpoint is null)
        {
            _voiceServerTcs.TrySetCanceled();
            return new CommandResult(CommandStatus.Disconnected);
        }
        
        // Initial connection.
        if (_voiceClient is null)
        {
            _voiceServerTcs.TrySetResult();
            return new CommandResult(CommandStatus.Success);
        }

        await ReconnectAsync();
        _voiceServerTcs.TrySetResult();
        
        return new CommandResult(CommandStatus.Success);

        async Task ReconnectAsync()
        {
            await _gate.WaitAsync();

            try
            {
                VoiceClient? oldVoiceClient = _voiceClient;
                Stream? oldVoiceStream = _voiceStream;

                _voiceClient = null;
                _voiceStream = null;

                if (oldVoiceStream is not null)
                {
                    await Sink.DetachAsync();
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

                VoiceClient voiceClient = new
                (
                    _userId,
                    _sessionId,
                    _endpoint!,
                    _guildId,
                    _channelId!.Value,
                    _token,
                    new VoiceClientConfiguration { Logger = _voiceLogger }
                );

                await voiceClient.StartAsync();
                await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

                Stream voiceStream = voiceClient.CreateVoiceStream();
                await Sink.AttachAsync(voiceStream);

                _voiceClient = voiceClient;
                _voiceStream = voiceStream;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private async Task<CommandResult> VoiceStateUpdateAsync(VoiceStateUpdateEventCommand eventCommand)
    {
        _userId = eventCommand.UserId;
        _channelId = eventCommand.ChannelId;
        _sessionId = eventCommand.SessionId;

        if (!_channelId.HasValue)
        {
            // Bot disconnected externally.
            await DisconnectCoreAsync();

            if (Disconnected is not null)
            {
                await Disconnected();
            }
            
            _voiceStateTcs.TrySetCanceled();
            return new CommandResult(CommandStatus.Disconnected);
        }
        
        _voiceStateTcs.TrySetResult();
        return new CommandResult(CommandStatus.Success);
    }

    private async Task<bool> WaitForValidVoiceAsync(TimeSpan timeout)
    {
        Task waitTask = Task.WhenAll(_voiceServerTcs.Task, _voiceStateTcs.Task);
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout));
        
        return completedTask == waitTask &&
               _voiceServerTcs.Task.IsCompletedSuccessfully &&
               _voiceStateTcs.Task.IsCompletedSuccessfully;
    }

    private async Task DisconnectCoreAsync()
    {
        await _gate.WaitAsync();

        try
        {
            await Sink.DetachAsync();

            if (_voiceStream is not null)
            {
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

            await _gatewayClient.UpdateVoiceStateAsync(
                new VoiceStateProperties(_guildId, null));

            _voiceServerTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _voiceStateTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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

        _voiceEvents.Writer.TryComplete();
        _commands.Writer.TryComplete();

        try
        {
            await _voiceEventLoopTask;
        }
        catch
        {
            // Ignore.
        }

        try
        {
            await _commandLoopTask;
        }
        catch
        {
            // Ignore.
        }
        
        await DisconnectCoreAsync();
    }
}
