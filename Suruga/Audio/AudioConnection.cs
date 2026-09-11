using System.Threading.Channels;
using NetCord.Gateway;
using NetCord.Logging;
using Suruga.Audio.Commands;
using Suruga.Audio.Commands.Connection;
using Suruga.Audio.Commands.Voice;
using Suruga.Audio.Primitives;

namespace Suruga.Audio;

internal sealed class AudioConnection : IAsyncDisposable
{
    internal AudioSink Sink { get; }

    private readonly VoiceHandshakeCoordinator _handshake = new();
    private readonly VoiceClientLifecycle _lifecycle;

    private readonly Channel<VoiceEventCommand> _voiceEvents = Channel.CreateUnbounded<VoiceEventCommand>();
    private readonly Channel<ConnectionCommand> _commands = Channel.CreateUnbounded<ConnectionCommand>();
    private readonly Task _voiceEventLoopTask;
    private readonly Task _commandLoopTask;

    private bool _isDisposed;

    public AudioConnection(GatewayClient gatewayClient, IVoiceLogger voiceLogger, ulong guildId)
    {
        Sink = new AudioSink();
        _lifecycle = new VoiceClientLifecycle(gatewayClient, voiceLogger, Sink, _handshake, guildId);

        _voiceEventLoopTask = HandleVoiceEventsAsync();
        _commandLoopTask = HandleCommandsAsync();
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
                    VoiceServerUpdateEventCommand voiceServer => await OnVoiceServerUpdateAsync(voiceServer),
                    VoiceStateUpdateEventCommand voiceState => await OnVoiceStateUpdateAsync(voiceState),
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
                    ConnectCommand connect => _isDisposed
                        ? new CommandResult(CommandStatus.Undefined)
                        : await _lifecycle.ConnectAsync(connect.VoiceChannelId),
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

    private async Task<CommandResult> OnVoiceServerUpdateAsync(VoiceServerUpdateEventCommand eventCommand)
    {
        bool valid = _handshake.OnVoiceServerUpdate(eventCommand.Endpoint, eventCommand.Token);

        if (!valid)
        {
            return new CommandResult(CommandStatus.Disconnected);
        }

        if (_lifecycle.IsConnected)
        {
            await _lifecycle.ReconnectAsync();
        }

        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> OnVoiceStateUpdateAsync(VoiceStateUpdateEventCommand eventCommand)
    {
        bool valid = _handshake.OnVoiceStateUpdate(eventCommand.UserId, eventCommand.ChannelId, eventCommand.SessionId);

        if (!valid)
        {
            await DisconnectAsync();

            return new CommandResult(CommandStatus.Disconnected);
        }

        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> DisconnectAsync()
    {
        await _lifecycle.DisconnectAsync();
        return new CommandResult(CommandStatus.Success);
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

        await _lifecycle.DisconnectAsync();
    }
}
