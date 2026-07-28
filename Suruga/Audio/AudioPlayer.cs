using NetCord;
using Suruga.Audio.Events;
using Suruga.Audio.Primitives;
using Suruga.Persistence;
using Suruga.Primitives;
using Suruga.Resolvers;

namespace Suruga.Audio;

internal sealed class AudioPlayer : IAsyncDisposable
{
	internal event Func<PlayerStateChangedEventArgs, Task>? PlayerStateChanged;
	
	internal TrackQueue Queue { get; }
	
	internal AudioPlaybackEngine Engine { get; }

	internal AudioPlaybackState State { get; private set; } = AudioPlaybackState.Idle;

	private readonly ulong _guildId;
	private readonly TrackResolverRouter _resolverRouter;
	private readonly TrackQueueStateRepository _repository;
	private readonly AsyncMutex _playbackMutex = new();

	private bool _isDisposed;

	internal AudioPlayer
	(
		ulong guildId,
		TrackResolverRouter resolverRouter,
		AudioPlaybackEngine engine,
		TrackQueueStateRepository repository
	)
	{
		_guildId = guildId;
		_resolverRouter = resolverRouter;
		_repository = repository;
		
		Engine = engine;
		Engine.TrackEnded += OnTrackEndedAsync;
		
		TrackQueueState? queueState = Task.Run(() => repository.Load(guildId)).Result;
		Queue = new TrackQueue(queueState);
	}

	internal async Task<int> PlayAsync(string input, GuildUser requestedBy, CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		TrackRequestContext context = TrackRequestContext.FromUser(requestedBy);
		Result<TrackSet> resolveResult = await _resolverRouter.ResolveAsync(input, context, token);

		if (!resolveResult.TryGetValue(out TrackSet? set) || set.IsEmpty)
		{
			return 0;
		}

		using (await _playbackMutex.EnterScopeAsync(token))
		{
			Queue.EnqueueRange(set.Tracks);
			await SaveStateAsync(token);

			if (State is AudioPlaybackState.Playing)
			{
				return set.Count;
			}

			if (Queue.HasCurrent)
			{
				Queue.ArchiveCurrentTrack();
			}

			if (Queue.TryDequeue(out Track? dequeuedTrack))
			{
				await StartTrackAsync(dequeuedTrack, token);
			}
			else if (Queue.CurrentTrack is Track currentTrack)
			{
				await StartTrackAsync(currentTrack, token);
			}
		}

		return set.Count;
	}

	internal async Task StopAsync(CancellationToken token = default)
	{
		using (await _playbackMutex.EnterScopeAsync(token))
		{
			Queue.Clear();
			await SaveStateAsync(token);
			
			await Engine.StopAsync(token);
			await TransitionAsync(AudioPlaybackState.Idle);
		}
	}

	internal Task PauseAsync()
	{
		if (State is AudioPlaybackState.Paused)
		{
			return Task.CompletedTask;
		}
		
		Engine.Pause();
		return TransitionAsync(AudioPlaybackState.Paused, Queue.CurrentTrack);
	}

	internal Task ResumeAsync()
	{
		if (State is AudioPlaybackState.Playing)
		{
			return Task.CompletedTask;
		}
		
		Engine.Resume();
		return TransitionAsync(AudioPlaybackState.Playing, Queue.CurrentTrack);
	}

	internal void Seek(TimeSpan position)
		=> Engine.Seek(position);

	internal async Task SkipAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		using (await _playbackMutex.EnterScopeAsync(token))
		{
			if (!Queue.TrySkip(out Track? nextTrack))
			{
				Queue.ArchiveCurrentTrack();
				await Engine.StopAsync(token);
				await TransitionAsync(AudioPlaybackState.Idle);
				
				return;
			}

			await StartTrackAsync(nextTrack, token);
			await SaveStateAsync(token);
		}
	}

	internal async Task RewindAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		using (await _playbackMutex.EnterScopeAsync(token))
		{
			if (!Queue.TryMovePrevious(out Track? previousTrack))
			{
				await Engine.StopAsync(token);
				return;
			}

			await StartTrackAsync(previousTrack, token);
			await SaveStateAsync(token);
		}
	}

	internal async Task LoopAsync(LoopMode mode = default, CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		using (await _playbackMutex.EnterScopeAsync(token))
		{
			// Cycle if no explicit loop mode is provided.
			if (mode == default)
			{
				Queue.CycleLoopMode();
			}
			else
			{
				Queue.LoopMode = mode;
			}
			
			await SaveStateAsync(token);
		}
	}

	internal async Task ShuffleAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		using (await _playbackMutex.EnterScopeAsync(token))
		{
			Queue.Shuffle();
			await SaveStateAsync(token);
		}
	}

	internal async Task ClearAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		using (await _playbackMutex.EnterScopeAsync(token))
		{
			Queue.Clear();
            await _repository.RemoveAsync(_guildId, token);
		}
	}
	
	public async ValueTask DisposeAsync()
	{
		if (_isDisposed)
		{
			return;
		}

		_isDisposed = true;
		Engine.TrackEnded -= OnTrackEndedAsync;
		
		try
		{
			await Engine.DisposeAsync();
		}
		catch
		{
			// Ignore exceptions during disposal.
		}

		_playbackMutex.Dispose();
	}

	/// <summary>
	/// Starts playback from persisted queue state when the engine is idle.
	/// </summary>
	internal async Task<bool> ResumeFromStoredQueueAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		using (await _playbackMutex.EnterScopeAsync(token))
		{
			if (State is AudioPlaybackState.Playing)
			{
				return false;
			}

			if (Queue.CurrentTrack is Track current)
			{
				await StartTrackAsync(current, token);
				return true;
			}
			else if (Queue.TryDequeue(out Track? track))
			{
				await StartTrackAsync(track, token);
				return true;
			}

			return false;
		}
	}

	private async Task StartTrackAsync(Track track, CancellationToken token = default)
	{
		await SaveStateAsync(token);
		await Engine.PlayAsync(track, token);
		await TransitionAsync(AudioPlaybackState.Playing, track);
	}

	private async Task OnTrackEndedAsync(Track track)
	{
		try
		{
			using (await _playbackMutex.EnterScopeAsync())
			{
				if (_isDisposed)
				{
					return;
				}

				if (Queue.TryMoveNext(out Track? nextTrack))
				{
					await StartTrackAsync(nextTrack);
				}
				else
				{
					Queue.ArchiveCurrentTrack();
					await TransitionAsync(AudioPlaybackState.Idle);
				}

				await SaveStateAsync();
			}
		}
		catch
		{
			// Ignore playback continuation failures.
		}
	}
	
	private async Task TransitionAsync(AudioPlaybackState state, Track? currentTrack = null)
	{
		if (State == state)
		{
			return;
		}

		State = state;

		if (PlayerStateChanged is not null)
		{
			await PlayerStateChanged(new PlayerStateChangedEventArgs(state, currentTrack, false));
		}
	}
	
	private async Task SaveStateAsync(CancellationToken token = default)
	{
		TrackQueueState state = new()
		{
			GuildId = _guildId,
			History = GetNonLocalTracks(Queue.History),
			Upcoming = GetNonLocalTracks(Queue.Upcoming),
			CurrentTrack = IsNotLocalTrack(Queue.CurrentTrack) ? Queue.CurrentTrack : null,
			LoopMode = Queue.LoopMode,
		};
		
		await _repository.SaveAsync(state, token);
	}

	private static List<Track> GetNonLocalTracks(IEnumerable<Track> tracks)
		=> tracks.Where(IsNotLocalTrack).ToList();

	private static bool IsNotLocalTrack(Track? track)
		=> track?.Platform is not TrackPlatform.Local;
}
