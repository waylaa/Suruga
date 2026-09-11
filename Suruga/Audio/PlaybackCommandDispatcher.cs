using System.Threading.Channels;
using Suruga.Audio.Commands.Playback;
using Suruga.Audio.Primitives;
using Suruga.PostProcessing;

namespace Suruga.Audio;

internal sealed class PlaybackCommandDispatcher : IAsyncDisposable
{
    private readonly Channel<PlaybackCommand> _commands = Channel.CreateUnbounded<PlaybackCommand>();
    private readonly Task _loopTask;
    private readonly TrackPlaybackEngine _engine;
    private readonly TrackQueue _queue;
    private readonly AudioPostProcessor _postProcessor;

    internal PlaybackCommandDispatcher(TrackPlaybackEngine engine, TrackQueue queue, AudioPostProcessor postProcessor)
    {
        _engine = engine;
        _queue = queue;
        _postProcessor = postProcessor;
        _loopTask = HandleAsync();
    }
    
    internal async Task<CommandResult> PostAsync(PlaybackCommand command, CancellationToken token = default)
    {
        await _commands.Writer.WriteAsync(command, token);

        await using (token.Register(() => command.SetCanceled(token)))
        {
            return await command.Task;
        }
    }
    
    private async Task HandleAsync()
    {
        await foreach (PlaybackCommand command in _commands.Reader.ReadAllAsync())
        {
            try
            {
                CommandResult result = command switch
                {
                    PlayAudioCommand play => await _engine.PlayAsync(play.Tracks),
                    StopAudioCommand => await _engine.StopAsync(),
                    PauseAudioCommand => await _engine.PauseAsync(),
                    ResumeAudioCommand => await _engine.ResumeAsync(),
                    SkipAudioCommand => await _engine.SkipAsync(),
                    RewindAudioCommand => await _engine.RewindAsync(),
                    ShuffleAudioCommand => _queue.TryShuffle() ? new CommandResult(CommandStatus.Success) : new CommandResult(CommandStatus.NotEnoughTracksToShuffle),
                    LoopAudioCommand loop => SetLoopMode(loop),
                    SeekAudioCommand seek => _engine.Seek(seek.Timestamp),
                    ClearAudioCommand => await ClearAsync(),
                    VolumeAudioCommand volume => SetVolume(volume),
                    SpeedAudioCommand speed => SetSpeed(speed),
                    PitchAudioCommand pitch => SetPitch(pitch),
                    RateAudioCommand rate => SetRate(rate),
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
    
    private async Task<CommandResult> ClearAsync()
    {
        if (!_queue.TryClear())
        {
            return new CommandResult(CommandStatus.NothingToClear);
        }

        await _queue.RemovePersistedStateAsync();
        return new CommandResult(CommandStatus.Success);
    }

    private CommandResult SetLoopMode(LoopAudioCommand command)
    {
        _queue.LoopMode = command.Mode ?? _queue.LoopMode switch
        {
            LoopMode.None => LoopMode.Track,
            LoopMode.Track => LoopMode.Queue,
            LoopMode.Queue => LoopMode.None,
            _ => _queue.LoopMode
        };

        return new CommandResult(CommandStatus.Success, LoopModeToString(_queue.LoopMode));

        static string LoopModeToString(LoopMode mode) => mode switch
        {
            LoopMode.None => "none",
            LoopMode.Track => "per-track",
            LoopMode.Queue => "per-queue",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private CommandResult SetVolume(VolumeAudioCommand command)
    {
        _postProcessor.SetGain(command.Value / 100f);
        return new CommandResult(CommandStatus.Success);
    }
    
    private CommandResult SetSpeed(SpeedAudioCommand command)
    {
        _postProcessor.SetTempo(command.Value);
        return new CommandResult(CommandStatus.Success);
    }
    
    private CommandResult SetPitch(PitchAudioCommand command)
    {
        _postProcessor.SetPitch(command.Value);
        return new CommandResult(CommandStatus.Success);
    }
    
    private CommandResult SetRate(RateAudioCommand command)
    {
        _postProcessor.SetRate(command.Value);
        return new CommandResult(CommandStatus.Success);
    }
    
    public async ValueTask DisposeAsync()
    {
        _commands.Writer.TryComplete();

        try
        {
            await _loopTask;
        }
        catch
        {
            // Ignore.
        }
    }
}
