using Microsoft.Extensions.Logging;
using Suruga.Audio.Primitives;
using Suruga.FFmpeg;
using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Resolvers.Sources;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class TrackPlaybackEngine : IAsyncDisposable
{
    internal AudioPlayerState State { get; private set; } = AudioPlayerState.Idle;
    
    private readonly TrackQueue _queue;
    private readonly TrackStreamResolverRouter _resolverRouter;
    private readonly ReadOnlyAudioByteStreamFactory _byteStreamFactory;
    private readonly AudioSink _sink;
    private readonly AudioPostProcessor _postProcessor;
    private readonly PlayerStateNotifier _stateNotifier;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<TrackPlaybackEngine> _logger;
    private readonly PauseTokenSource _pauseTokenSource = new();
    private readonly Lock _lock = new();

    private CancellationTokenSource? _playbackCts;
    private CancellationTokenSource? _trackCts;
    private Task? _playbackLoopTask;
    private AudioDecoder? _decoder;
    
    private bool _isDisposed;
    
    internal TrackPlaybackEngine
    (
        TrackQueue queue,
        TrackStreamResolverRouter resolverRouter,
        ReadOnlyAudioByteStreamFactory byteStreamFactory,
        AudioSink sink,
        AudioPostProcessor postProcessor,
        PlayerStateNotifier stateNotifier,
        ILoggerFactory loggerFactory
    )
    {
        _queue = queue;
        _resolverRouter = resolverRouter;
        _byteStreamFactory = byteStreamFactory;
        _sink = sink;
        _postProcessor = postProcessor;
        _stateNotifier = stateNotifier;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<TrackPlaybackEngine>();
    }
    
    internal async Task<CommandResult> PlayAsync(TrackSet trackSet)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (trackSet.IsEmpty)
        {
            return new CommandResult(CommandStatus.NoTracks);
        }

        foreach (Track track in trackSet.Tracks)
        {
            _queue.Add(track);
        }

        if (State is not AudioPlayerState.Idle)
        {
            return new CommandResult(CommandStatus.Success);
        }

        if (_playbackLoopTask is not null)
        {
            await _playbackLoopTask;
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _playbackCts?.Dispose();
        _trackCts?.Dispose();

        _playbackCts = new CancellationTokenSource();
        _playbackLoopTask = PlaybackLoopAsync(_playbackCts.Token);

        return new CommandResult(CommandStatus.Success);
    }

    internal async Task<CommandResult> StopAsync()
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

    internal async Task<CommandResult> PauseAsync()
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
        await ChangeStateAsync(AudioPlayerState.Paused, _queue.CurrentTrack);

        return new CommandResult(CommandStatus.Success);
    }

    internal async Task<CommandResult> ResumeAsync()
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

        _pauseTokenSource.IsPaused = false;
        await ChangeStateAsync(AudioPlayerState.Playing, _queue.CurrentTrack);

        return new CommandResult(CommandStatus.Success);
    }

    internal async Task<CommandResult> SkipAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (State is AudioPlayerState.Idle || !_queue.TryMoveToNext(true))
        {
            return new CommandResult(CommandStatus.NothingToSkip);
        }

        if (_trackCts is not null)
        {
            await _trackCts.CancelAsync();
        }

        return new CommandResult(CommandStatus.Success);
    }

    internal async Task<CommandResult> RewindAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (State is AudioPlayerState.Idle || !_queue.TryMoveToPrevious())
        {
            return new CommandResult(CommandStatus.NothingToRewind);
        }

        if (_trackCts is not null)
        {
            await _trackCts.CancelAsync();
        }

        return new CommandResult(CommandStatus.Success);
    }

    internal CommandResult Seek(TimeSpan timestamp)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using (_lock.EnterScope())
        {
            if (State is AudioPlayerState.Idle)
            {
                return new CommandResult(CommandStatus.NothingToSeek);
            }

            return _decoder?.TrySeek(timestamp) == true
                ? new CommandResult(CommandStatus.Success)
                : new CommandResult(CommandStatus.UnableToSeek);
        }
    }

    private async Task PlaybackLoopAsync(CancellationToken token)
    {
        while (_queue.TryGetCurrent(out Track? track) && !token.IsCancellationRequested)
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

                foreach (AudioFrameBuffer chunk in pipeline.GetAudioChunks(trackCts.Token))
                {
                    await _pauseTokenSource.Token
                        .WaitWhilePausedAsync(() => _sink.FlushAsync(CancellationToken.None))
                        .WaitAsync(trackCts.Token);

                    await _sink.WriteAsync(chunk.Buffer, trackCts.Token);
                }

                await _sink.FlushAsync(trackCts.Token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break; // Stop/DisposeAsync called.
            }
            catch (OperationCanceledException)
            {
                // Skip/Rewind already moved the queue; do not advance again.
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);
                await ChangeStateAsync(AudioPlayerState.Idle, track, ex);
            }
            finally
            {
                await _queue.SaveAsync(CancellationToken.None);
                _trackCts = null;
            }

            if (!_queue.TryMoveToNext())
            {
                break;
            }
        }

        await ChangeStateAsync(AudioPlayerState.Idle);
    }

    private async Task ChangeStateAsync(AudioPlayerState state, Track? currentTrack = null, Exception? error = null)
    {
        if (_isDisposed || State == state)
        {
            return;
        }

        State = state;
        await _stateNotifier.NotifyAsync(state, currentTrack, error);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

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

            using (_lock.EnterScope())
            {
                _decoder?.Dispose();
                _decoder = null;
            }
        }
        catch
        {
            // Ignore.
        }
    }
}
