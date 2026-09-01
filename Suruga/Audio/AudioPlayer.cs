using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Suruga.Audio.Commands.Playback;
using Suruga.Audio.Primitives;
using Suruga.FFmpeg;
using Suruga.FFmpeg.Primitives;
using Suruga.Persistence;
using Suruga.PostProcessing;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Resolvers.Sources;
using Suruga.Transport;
using Channel = System.Threading.Channels.Channel;

namespace Suruga.Audio;

internal sealed class AudioPlayer : IAsyncDisposable
{
    internal event Func<AudioPlayerState, Track?, Exception?, Task>? PlayerStateChanged;

    internal TrackQueue Queue { get; }

    internal AudioPlayerState State { get; private set; } = AudioPlayerState.Idle;

    private readonly TrackStreamResolverRouter _resolverRouter;
    private readonly ReadOnlyAudioByteStreamFactory _byteStreamFactory;
    private readonly TrackQueueStateRepository _repository;
    private readonly AudioSink _sink;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ulong _guildId;
    
    private readonly ILogger<AudioPlayer> _logger;
    private readonly AudioPostProcessor _postProcessor = new();
    private readonly Lock _lock = new();
    
    private readonly Channel<PlaybackCommand> _commands = Channel.CreateUnbounded<PlaybackCommand>();
    private readonly Task _commandLoopTask;

    private readonly PauseTokenSource _pauseTokenSource = new();
    private CancellationTokenSource? _playbackCts;
    private CancellationTokenSource? _trackCts;
    
    private Task? _playbackLoopTask;
    private AudioDecoder? _decoder;

    private bool _isDisposed;
    
    internal AudioPlayer
    (
        TrackStreamResolverRouter resolverRouter,
        ReadOnlyAudioByteStreamFactory byteStreamFactory,
        TrackQueueStateRepository repository,
        AudioSink sink,
        ILoggerFactory loggerFactory,
        ulong guildId
    )
    {
        _resolverRouter = resolverRouter;
        _byteStreamFactory = byteStreamFactory;
        _repository = repository;
        _sink = sink;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<AudioPlayer>();
        _guildId = guildId;
        
        Queue = new TrackQueue(_repository, guildId);
        _commandLoopTask = HandleAsync();
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
                    PlayAudioCommand play => Play(play),
                    StopAudioCommand => await StopAsync(),
                    PauseAudioCommand => await PauseAsync(),
                    ResumeAudioCommand => await ResumeAsync(),
                    SkipAudioCommand => await SkipAsync(),
                    RewindAudioCommand => await RewindAsync(),
                    ShuffleAudioCommand => Shuffle(),
                    LoopAudioCommand loop => Loop(loop),
                    SeekAudioCommand seek => Seek(seek),
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

    private CommandResult Play(PlayAudioCommand command)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        TrackSet trackSet = command.Tracks;

        if (trackSet.IsEmpty)
        {
            // This should generally not happen here as it is handled by the command module.
            return new CommandResult(CommandStatus.NoTracks); 
        }
        
        foreach (Track track in trackSet.Tracks)
        {
            Queue.Add(track);
        }

        if (State is not AudioPlayerState.Idle)
        {
            return new CommandResult(CommandStatus.Success);
        }
        
        _playbackCts?.Dispose();
        _trackCts?.Dispose();

        _playbackCts = new CancellationTokenSource();
        _playbackLoopTask = PlaybackLoopAsync(_playbackCts.Token);
        
        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> StopAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (State is AudioPlayerState.Idle)
        {
            return new CommandResult(CommandStatus.AlreadyStopped);
        }

        if (_playbackCts is not null)
        {
            await _playbackCts.CancelAsync();
        }

        if (_playbackLoopTask is not null)
        {
            await _playbackLoopTask;
        }
        
        await ChangeStateAsync(AudioPlayerState.Idle);
        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> PauseAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (State is AudioPlayerState.Idle)
        {
            return new CommandResult(CommandStatus.NothingToPause);
        }

        if (State is AudioPlayerState.Paused)
        {
            return new CommandResult(CommandStatus.AlreadyPaused);
        }

        _pauseTokenSource.IsPaused = true;
        await ChangeStateAsync(AudioPlayerState.Paused, Queue.CurrentTrack);

        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> ResumeAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (State is AudioPlayerState.Idle)
        {
            return new CommandResult(CommandStatus.NothingToResume);
        }

        if (State is AudioPlayerState.Playing)
        {
            return new CommandResult(CommandStatus.AlreadyPlaying);
        }

        if (State is AudioPlayerState.Idle && Queue is { HasCurrent: true, HasNext: true })
        {
            _playbackCts?.Dispose();
            _trackCts?.Dispose();

            _playbackCts = new CancellationTokenSource();
            _playbackLoopTask = PlaybackLoopAsync(_playbackCts.Token);
        }
        else
        {
            _pauseTokenSource.IsPaused = false;
        }

        await ChangeStateAsync(AudioPlayerState.Playing, Queue.CurrentTrack);
        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> SkipAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (State is AudioPlayerState.Idle || !Queue.TryMoveToNext(true))
        {
            return new CommandResult(CommandStatus.NothingToSkip);
        }
        
        if (_trackCts is not null)
        {
            await _trackCts.CancelAsync();
        }

        return new CommandResult(CommandStatus.Success);
    }

    private async Task<CommandResult> RewindAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (State is AudioPlayerState.Idle || !Queue.TryMoveToPrevious())
        {
            return new CommandResult(CommandStatus.NothingToRewind);
        }

        if (_trackCts is not null)
        {
            await _trackCts.CancelAsync();
        }
        
        return new CommandResult(CommandStatus.Success);
    }

    private CommandResult Shuffle()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return Queue.TryShuffle()
            ? new CommandResult(CommandStatus.Success)
            : new CommandResult(CommandStatus.NotEnoughTracksToShuffle);
    }

    private CommandResult Loop(LoopAudioCommand command)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        if (command.Mode is LoopMode mode)
        {
            Queue.LoopMode = mode;
        }
        else
        {
            Queue.LoopMode = Queue.LoopMode switch
            {
                LoopMode.None => LoopMode.Track,
                LoopMode.Track => LoopMode.Queue,
                LoopMode.Queue => LoopMode.None,
                _ => Queue.LoopMode
            };
        }
        
        return new CommandResult(CommandStatus.Success, LoopModeToString(Queue.LoopMode));
        
        static string LoopModeToString(LoopMode mode) => mode switch
        {
            LoopMode.None => "none",
            LoopMode.Track => "per-track",
            LoopMode.Queue => "per-queue",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private CommandResult Seek(SeekAudioCommand command)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using (_lock.EnterScope())
        {
            if (State is AudioPlayerState.Idle)
            {
                return new CommandResult(CommandStatus.NothingToSeek);
            }
            
            _postProcessor.Reset();
            
            return _decoder?.TrySeek(command.Timestamp) == true
                ? new CommandResult(CommandStatus.Success)
                : new CommandResult(CommandStatus.UnableToSeek);
        }
    }

    private async Task<CommandResult> ClearAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!Queue.TryClear())
        {
            return new CommandResult(CommandStatus.NothingToClear);
        }
        
        await _repository.RemoveAsync(_guildId);
        return new CommandResult(CommandStatus.Success);
    }

    private CommandResult SetVolume(VolumeAudioCommand command)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _postProcessor.SetGain(command.Value / 100.0);
        return new CommandResult(CommandStatus.Success);
    }

    private CommandResult SetSpeed(SpeedAudioCommand command)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _postProcessor.SetTempo(command.Value);
        return new CommandResult(CommandStatus.Success);
    }

    private CommandResult SetPitch(PitchAudioCommand command)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _postProcessor.SetPitch(command.Value);
        return new CommandResult(CommandStatus.Success);
    }

    private CommandResult SetRate(RateAudioCommand command)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _postProcessor.SetRate(command.Value);
        return new CommandResult(CommandStatus.Success);
    }

    private async Task PlaybackLoopAsync(CancellationToken token)
    {
        while (Queue.TryGetCurrent(out Track? track) && !token.IsCancellationRequested)
        {
            await ChangeStateAsync(AudioPlayerState.Playing, track);

            using (_lock.EnterScope())
            {
                _decoder?.Dispose();
                _decoder = null;
            }
            
            using CancellationTokenSource trackCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _trackCts = trackCts;
            
            try
            {
                StreamSource source = await _resolverRouter.ResolveStreamUriAsync(track, trackCts.Token);
                await using ReadOnlyAudioByteStream byteStream = _byteStreamFactory.Create(source);

                using (_lock.EnterScope())
                {
                    _decoder = new AudioDecoder(byteStream, _loggerFactory.CreateLogger<AudioDecoder>());
                }

                if (track.StartPosition is TimeSpan startPosition)
                {
                    _decoder.TrySeek(startPosition);
                }

                AudioPipeline pipeline = new(_decoder, _postProcessor);

                foreach (AudioChunk chunk in pipeline.GetAudioChunks(trackCts.Token))
                {
                    await _pauseTokenSource.Token
                        .WaitWhilePausedAsync(() => _sink.FlushAsync(CancellationToken.None))
                        .WaitAsync(trackCts.Token);
                    
                    try
                    {
                        await _sink.WriteAsync(chunk.Buffer, trackCts.Token);
                    }
                    finally
                    {
                        chunk.Dispose();
                    }
                }

                await _sink.FlushAsync(trackCts.Token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break; // Stop/DisposeAsync called.
            }
            catch (OperationCanceledException)
            {
                // Skip/Rewind called.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);
                
                // The plain idle state call does not get executed because both
                // states are the same.
                await ChangeStateAsync(AudioPlayerState.Idle, track, ex);
            }
            finally
            {
                await Queue.SaveAsync(trackCts.Token);
                _trackCts = null;
            }

            if (!Queue.TryMoveToNext())
            {
                break;
            }
        }
        
        await ChangeStateAsync(AudioPlayerState.Idle);
    }

    private async Task ChangeStateAsync(AudioPlayerState state, Track? currentTrack = null, Exception? error = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        if (State == state)
        {
            return;
        }
        
        State = state;

        if (PlayerStateChanged is not null)
        {
            await PlayerStateChanged(state, currentTrack, error);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _commands.Writer.TryComplete();

        try
        {
            if (_playbackCts is not null)
            {
                await _playbackCts.CancelAsync();
            }

            if (_playbackLoopTask is not null)
            {
                await _playbackLoopTask;
            }

            await _commandLoopTask;
            _decoder?.Dispose();
        }
        catch
        {
            // Ignore.
        }
        
        // Mark as disposed at the end of disposal to prevent ChangeStateAsync throwing
        // ObjectDisposedException when the player changes its state during disposal or cancellation.
        _isDisposed = true; 
    }
}
