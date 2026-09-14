using Suruga.Audio.Primitives;
using Suruga.Common;
using Suruga.FFmpeg;
using Suruga.FFmpeg.Primitives;
using Suruga.Persistence;
using Suruga.PostProcessing;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Resolvers.Sources;
using Suruga.Transport;

namespace Suruga.Audio;

internal sealed class AudioPlayer
(
    TrackStreamResolverRouter resolverRouter,
    ReadOnlyAudioByteStreamFactory byteStreamFactory,
    TrackQueueRepository repository,
    AudioSink sink,
    ulong guildId
) : IAsyncDisposable
{
    internal event Func<AudioPlayerState, Track?, Exception?, Task>? PlayerStateChanged;

    internal Track? CurrentTrack => Queue.CurrentTrack;
    
    internal TrackQueue Queue { get; } = new(repository.Load(guildId));

    internal AudioPlayerState State
    {
        get
        {
            using (_stateLock.EnterScope())
            {
                return field;
            }
        }
        set
        {
            using (_stateLock.EnterScope())
            {
                field = value;
            }
        }
    } = AudioPlayerState.Idle;

    private readonly AudioPostProcessor _postProcessor = new();
    private readonly PlaybackPauseController _pauseController = new();
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly Lock _stateLock = new();
    
    private readonly CancellationTokenSource _lifetimeCts = new();

    private CancellationTokenSource? _sessionCts;
    private CancellationTokenSource? _trackCts;
    private AudioDecoder? _activeDecoder;
    private Task? _playbackLoopTask;
    
    private volatile bool _isDisposed;

    internal Task<CommandResult> PlayAsync(TrackSet trackSet)
    {
        return RunCommandAsync(async () =>
        {
            if (trackSet.IsEmpty)
            {
                Logger.Debug<AudioPlayer>($"PlayAsync received an empty track set in guild {guildId}");
                return new CommandResult(CommandStatus.NoTracks);
            }

            foreach (Track track in trackSet.Tracks)
            {
                Queue.Add(track);
            }
            
            Logger.Trace<AudioPlayer>($"Queued {trackSet.Tracks.Count} track(s) in guild {guildId}");
            await PersistQueueAsync();
            
            if (State is AudioPlayerState.Idle)
            {
                StartPlaybackLoop();
            }

            return new CommandResult(CommandStatus.Success);
        });
    }

    internal Task<CommandResult> StopAsync()
    {
        return RunCommandAsync(async () =>
        {
            if (State is AudioPlayerState.Idle)
            {
                return new CommandResult(CommandStatus.AlreadyStopped);
            }

            await StopPlaybackLoopAsync();

            Queue.Reset();
            await PersistQueueAsync();
            
            return new CommandResult(CommandStatus.Success);
        });
    }

    internal Task<CommandResult> PauseAsync()
    {
        return RunCommandAsync(async () =>
        {
            if (State is AudioPlayerState.Idle)
            {
                return new CommandResult(CommandStatus.NothingToPause);
            }

            if (State is AudioPlayerState.Paused)
            {
                return new CommandResult(CommandStatus.AlreadyPaused);
            }

            _pauseController.Pause();
            await SetStateAsync(AudioPlayerState.Paused, Queue.CurrentTrack);
            
            return new CommandResult(CommandStatus.Success);
        });
    }

    internal Task<CommandResult> ResumeAsync()
    {
        return RunCommandAsync(async () =>
        {
            if (State is AudioPlayerState.Idle)
            {
                // return new CommandResult(CommandStatus.NothingToResume);

                if (Queue.CurrentTrack is null)
                {
                    return new CommandResult(CommandStatus.NothingToResume);
                }
                
                StartPlaybackLoop();
                return new CommandResult(CommandStatus.Success);
            }

            if (State is not AudioPlayerState.Paused)
            {
                return new CommandResult(CommandStatus.AlreadyPlaying);
            }

            _pauseController.Resume();
            await SetStateAsync(AudioPlayerState.Playing, Queue.CurrentTrack);

            return new CommandResult(CommandStatus.Success);
        });
    }

    internal Task<CommandResult> SkipAsync()
    {
        return RunCommandAsync(async () =>
        {
            if (State is AudioPlayerState.Idle || !Queue.TryMoveToNext(out _))
            {
                return new CommandResult(CommandStatus.NothingToSkip);
            }
            
            CancelCurrentTrack();
            await PersistQueueAsync();
            
            return new CommandResult(CommandStatus.Success);
        });
    }
    
    internal Task<CommandResult> RewindAsync()
    {
        return RunCommandAsync(async () =>
        {
            if (State is AudioPlayerState.Idle || !Queue.TryMoveToPrevious(out _))
            {
                return new CommandResult(CommandStatus.NothingToRewind);
            }

            CancelCurrentTrack();
            await PersistQueueAsync();
            
            return new CommandResult(CommandStatus.Success);
        });
    }

    internal Task<CommandResult> SeekAsync(TimeSpan timestamp)
    {
        return RunCommandAsync(async () =>
        {
            if (State is AudioPlayerState.Idle || _activeDecoder?.TrySeek(timestamp) != true)
            {
                return new CommandResult(CommandStatus.NothingToSeek);
            }

            _postProcessor.Reset();
            await sink.FlushAsync();
            
            return new CommandResult(CommandStatus.Success);
        });
    }

    internal Task<CommandResult> LoopAsync(LoopMode? mode = null)
    {
        return RunCommandAsync(() =>
        {
            Queue.LoopMode = mode ?? Queue.LoopMode switch
            {
                LoopMode.None => LoopMode.Track,
                LoopMode.Track => LoopMode.Queue,
                LoopMode.Queue => LoopMode.None,
                _ => Queue.LoopMode
            };

            string modeLabel = Queue.LoopMode switch
            {
                LoopMode.None => "none",
                LoopMode.Track => "per-track",
                LoopMode.Queue => "per-queue",
                _ => "unknown"
            };

            return Task.FromResult(new CommandResult(CommandStatus.Success, modeLabel));
        });
    }

    internal Task<CommandResult> ShuffleAsync()
    {
        return RunCommandAsync(async () =>
        {
            if (!Queue.Shuffle())
            {
                return new CommandResult(CommandStatus.NotEnoughTracksToShuffle);
            }

            await PersistQueueAsync();
            return new CommandResult(CommandStatus.Success);
        });
    }

    internal Task<CommandResult> ClearAsync(CancellationToken token = default)
    {
        return RunCommandAsync(async () =>
        {
            if (!Queue.Clear())
            {
                return new CommandResult(CommandStatus.NothingToClear);
            }

            await repository.RemoveAsync(guildId, token);
            return new CommandResult(CommandStatus.Success);
        });
    }

    internal void SetVolume(int percent)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        _postProcessor.SetGain(percent / 100f);
        Logger.Trace<AudioPlayer>($"Volume set to {percent}% in guild {guildId}");
    }

    internal void SetSpeed(float value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        _postProcessor.SetTempo(value);
        Logger.Trace<AudioPlayer>($"Speed set to {value}x in guild {guildId}");
    }

    internal void SetPitch(float value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        _postProcessor.SetPitch(value);
        Logger.Trace<AudioPlayer>($"Pitch set to {value}x in guild {guildId}");
    }

    internal void SetRate(float value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        _postProcessor.SetRate(value);
        Logger.Trace<AudioPlayer>($"Rate set to {value}x in guild {guildId}. Speed and pitch have been reset.");
    }

    private async Task<CommandResult> RunCommandAsync(Func<Task<CommandResult>> commandOperation)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _commandGate.WaitAsync();

        try
        {
            return await commandOperation();
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private async Task PersistQueueAsync(CancellationToken token = default)
    {
        try
        {
            TrackQueue.TrackQueueSnapshot snapshot = Queue.GetSnapshot();

            List<Track> persistedTracks = new(snapshot.Tracks.Count);
            int persistedTrackCurrentIndex = -1;

            for (int i = 0; i < snapshot.Tracks.Count; i++)
            {
                Track track = snapshot.Tracks[i];
                
                if (track.Platform is TrackPlatform.Local)
                {
                    continue;
                }

                if (i == snapshot.CurrentTrackIndex)
                {
                    persistedTrackCurrentIndex = persistedTracks.Count;
                }
                
                persistedTracks.Add(track);
            }

            snapshot = snapshot with
            {
                Tracks = persistedTracks,
                CurrentTrackIndex = persistedTrackCurrentIndex
            };

            await repository.SaveAsync(guildId, snapshot, token);
        }
        catch (Exception ex)
        {
            Logger.Error<AudioPlayer>(ex, $"Failed to persist queue state for guild {guildId}");
        }
    }

    private void StartPlaybackLoop()
    {
        _sessionCts?.Dispose();
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        _playbackLoopTask = Task.Run(() => PlaybackLoopAsync(_sessionCts.Token));
    }

    private async Task StopPlaybackLoopAsync()
    {
        if (_sessionCts is not null)
        {
            await _sessionCts.CancelAsync();
        }

        if (_playbackLoopTask is not null)
        {
            try
            {
                await _playbackLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error<AudioPlayer>(ex, $"Error stopping playback loop in guild {guildId}");
            }
            
            _playbackLoopTask = null;
        }

        await SetStateAsync(AudioPlayerState.Idle);
    }
    
    private async Task PlaybackLoopAsync(CancellationToken sessionToken)
    {
        try
        {
            while (!sessionToken.IsCancellationRequested && Queue.CurrentTrack is Track currentTrack)
            {
                await PersistQueueAsync(sessionToken);
                await SetStateAsync(AudioPlayerState.Playing, currentTrack);

                using CancellationTokenSource trackCts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
                _trackCts = trackCts;

                bool isPlayedToCompletion = false;

                try
                {
                    await PlaySingleTrackAsync(currentTrack, trackCts.Token);
                    isPlayedToCompletion = true;
                }
                catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    Logger.Debug<AudioPlayer>($"Track '{currentTrack.Title}' skipped/cancelled in guild {guildId}");
                }
                catch (Exception ex)
                {
                    Logger.Error<AudioPlayer>(ex, $"Error playing track '{currentTrack.Title}' in guild {guildId}");
                    await SetStateAsync(AudioPlayerState.Idle, currentTrack, ex);
                }
                finally
                {
                    _trackCts = null;
                }

                if (isPlayedToCompletion)
                {
                    if (!Queue.TryAdvanceAfterCompletion(out _))
                    {
                        Logger.Debug<AudioPlayer>($"No more tracks in queue for guild {guildId}. Ending playback loop.");
                        break;
                    }
                }
            }
        }
        finally
        {
            await SetStateAsync(AudioPlayerState.Idle);
        }
    }

    private async Task PlaySingleTrackAsync(Track track, CancellationToken token)
    {
        Logger.Trace<AudioPlayer>($"Starting playback for track '{track.Title}' in guild {guildId}");

        StreamSource source = await resolverRouter.ResolveStreamUriAsync(track, token);
        await using ReadOnlyAudioByteStream byteStream = byteStreamFactory.Create(source);

        using AudioDecoder decoder = new(byteStream);
        _activeDecoder = decoder;
        
        try
        {
            AudioPipeline pipeline = new(decoder, _postProcessor, sink);

            await foreach (AudioFramebuffer frame in pipeline.GetAudioFrameBuffers(token))
            {
                await _pauseController.WaitWhilePausedAsync(async () => await sink.FlushAsync(CancellationToken.None), token);
                await sink.WriteAsync(frame.Buffer, token);
            }
        }
        finally
        {
            _activeDecoder = null;
        }
    }

    private void CancelCurrentTrack()
        => _trackCts?.Cancel();

    private async Task SetStateAsync(AudioPlayerState newState, Track? track = null, Exception? error = null)
    {
        if (_isDisposed && newState is not AudioPlayerState.Idle)
        {
            return;
        }
        
        AudioPlayerState oldState;

        using (_stateLock.EnterScope())
        {
            if (State == newState)
            {
                return;
            }
            
            oldState = State;
            State = newState;
        }

        Logger.Debug<AudioPlayer>($"Changing state from {oldState} to {newState} in guild {guildId}");

        if (PlayerStateChanged is not null)
        {
            await PlayerStateChanged(newState, track, error);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Logger.Debug<AudioPlayer>($"Disposing audio player in guild {guildId}");

        await _lifetimeCts.CancelAsync();

        if (_playbackLoopTask is not null)
        {
            try
            {
                await _playbackLoopTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                Logger.Warning<AudioPlayer>($"Playback loop did not exit within the shutdown timeout in guild {guildId}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error<AudioPlayer>(ex, $"Error while waiting for the playback loop to exit in guild {guildId}");
            }
        }

        _lifetimeCts.Dispose();
        _sessionCts?.Dispose();
        _trackCts?.Dispose();
        _commandGate.Dispose();
    }
}
