using Microsoft.Extensions.Logging;
using Suruga.Audio.Decode;
using Suruga.Audio.Decode.Primitives;
using Suruga.Audio.Primitives;
using Suruga.Primitives;
using Suruga.Resolvers;
using Suruga.Transport;

namespace Suruga.Audio;

/// <summary>
/// An audio playback engine that decodes, processes, and streams audio tracks
/// to a Discord voice connection.
/// </summary>
internal sealed partial class AudioPlaybackEngine : VolatileAsyncDisposable
{
	/// <summary>
	/// Fires when a track finishes playback successfully or after an error.
	/// </summary>
	internal event Func<Track, Task>? TrackEnded;

	/// <summary>
	/// Fires when a track fails during playback.
	/// </summary>
	internal event Func<Track, Exception, Task>? TrackErrored;

	/// <summary>
	/// Gets the active audio connection used for voice output.
	/// </summary>
	internal AudioConnection Connection { get; }

	/// <summary>
	/// Gets the post processor.
	/// </summary>
	internal AudioPostProcessor PostProcessor { get; } = new();

	private readonly TrackStreamResolverRouter _streamResolverRouter;
	private readonly ReadOnlyAudioByteStreamFactory _byteStreamFactory;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ILogger<AudioPlaybackEngine> _logger;
	
	private readonly VolatileDisposableResource<AudioDecoder> _decoderResource = new();
	private readonly AsyncManualResetEvent _pauseSignal = new();
	private readonly AsyncMutex _playbackMutex = new();
	private readonly CancellableAsyncWork _playback = new();

	/// <summary>
	/// Initializes a new playback engine instance.
	/// </summary>
	/// <param name="connection">The audio connection used for output.</param>
	/// <param name="streamResolverRouter">Resolves track stream sources.</param>
	/// <param name="byteStreamFactory">Factory for creating audio byte streams.</param>
	/// <param name="loggerFactory">Factory used to create loggers.</param>
	internal AudioPlaybackEngine
	(
		AudioConnection connection,
		TrackStreamResolverRouter streamResolverRouter,
		ReadOnlyAudioByteStreamFactory byteStreamFactory,
		ILoggerFactory loggerFactory
	)
	{
		Connection = connection;
		_streamResolverRouter = streamResolverRouter;
		_byteStreamFactory = byteStreamFactory;
		_loggerFactory = loggerFactory;
		_logger = _loggerFactory.CreateLogger<AudioPlaybackEngine>();
		
		_pauseSignal.Set(); // Playback starts unpaused.
	}

	/// <summary>
	/// Starts playback of the specified track.
	/// </summary>
	/// <param name="track">The track to play.</param>
	/// <param name="token">A cancellation token.</param>
	internal async Task PlayAsync(Track track, CancellationToken token = default)
	{
		using (await _playbackMutex.EnterScopeAsync(token))
		{
			ThrowIfDisposed();
			await StopCoreAsync();
			
			await _playback.StartAsync(ct => RunPlaybackAsync(track, ct), token);
		}
	}

	/// <summary>
	/// Stops playback and resets decoder and post-processing state.
	/// </summary>
	/// <param name="token">A cancellation token.</param>
	internal async Task StopAsync(CancellationToken token = default)
	{
		using (await _playbackMutex.EnterScopeAsync(token))
		{
			ThrowIfDisposed();
			await StopCoreAsync();
			
			if (_decoderResource.Current is AudioDecoder decoder)
			{
				decoder.Flush();
				decoder.Dispose();
			}

			_decoderResource.Clear();
			PostProcessor.Reset();
			
			await Connection.Sink.FlushAsync(token);
		}
	}

	/// <summary>
	/// Pauses playback.
	/// </summary>
	internal void Pause()
	{
		ThrowIfDisposed();
		_pauseSignal.Reset();
	}

	/// <summary>
	/// Resumes playback.
	/// </summary>
	internal void Resume()
	{
		ThrowIfDisposed();
		_pauseSignal.Set();
	}

	/// <summary>
	/// Seeks the currently playing track to the specified position.
	/// </summary>
	/// <param name="position">The target playback position.</param>
	internal void Seek(TimeSpan position)
	{
		ThrowIfDisposed();

		if (_decoderResource.Current is not AudioDecoder decoder)
		{
			return;
		}

		try
		{
			decoder.Seek(position);
		}
		catch (ObjectDisposedException)
		{
            // Note: A race can happen when leaving/stopping playback and at the same time executing Seek().
            // Ignore. 
        }
    }

	protected override async ValueTask DisposeCoreAsync()
	{
		try
		{
			await StopCoreAsync();
		}
		catch
		{
			// Ignore exceptions during disposal.
		}
		
		_decoderResource.Clear();
		await Connection.DisposeAsync();
		PostProcessor.Reset();
		_playbackMutex.Dispose();
	}

	/// <summary>
	/// Executes the playback loop for a single track.
	/// </summary>
	/// <param name="track">The track being played.</param>
	/// <param name="token">A cancellation token.</param>
	private async Task RunPlaybackAsync(Track track, CancellationToken token)
	{
		try
		{
			StreamSource source = await _streamResolverRouter.ResolveStreamUriAsync(track, token);
			await using ReadOnlyAudioByteStream byteStream = _byteStreamFactory.Create(source);
			AudioDecoder decoder = new(byteStream, _loggerFactory.CreateLogger<AudioDecoder>());

			_decoderResource.Replace(decoder);

			if (track.StartPosition is TimeSpan startPosition)
			{
				decoder.Seek(startPosition);
			}

			while (!token.IsCancellationRequested)
			{
				if (!decoder.TryDecodeFrame(out IAudioFrameBuffer? frame))
				{
					PostProcessor.SignalEndOfStream();
					break;
				}

				if (!PostProcessor.TryPostProcessFrame(frame, out IAudioFrameBuffer? postProcessedFrame))
				{
					continue;
				}

				using (postProcessedFrame)
				{
					await Connection.Sink.WriteAsync(postProcessedFrame.Buffer, token);
				}
				
				// Let the sink write any available frames before pausing and flushing.
				await _pauseSignal.WaitAsync(ct => Connection.Sink.FlushAsync(ct), token);
			}
			
			// Flush.
			while (!token.IsCancellationRequested)
			{
				if (!PostProcessor.TryFlush(out IAudioFrameBuffer? flushedFrame))
				{
					break;
				}

				using (flushedFrame)
				{
					await Connection.Sink.WriteAsync(flushedFrame.Buffer, token);
				}
				
				await _pauseSignal.WaitAsync(token: token);
			}
			
			await Connection.Sink.FlushAsync(token);
			PostProcessor.Reset();
			
			// Wrap in Task.Run to prevent the playback task from awaiting itself.
			// Currently, in order to advance to the next track we do this:
			// TrackEnded -> OnTrackEnded -> StartTrackAsync -> PlayAsync
			// -> StopCoreAsync -> await _playbackTask. This just causes a deadlock
			// if not wrapped in Task.Run, same thing for invoking the events below.
			// Awaiting the tasks also causes the same issue.
			if (TrackEnded is not null)
			{
				_ = Task.Run(() => TrackEnded.Invoke(track), token);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			LogTrackError(track.Id, track.Title, ex);
			
            if (TrackErrored is not null)
			{
				_ = Task.Run(() => TrackErrored.Invoke(track, ex), token);
			}
			
			if (TrackEnded is not null)
			{
				_ = Task.Run(() => TrackEnded.Invoke(track), token);
			}
		}
	}

	/// <summary>
	/// Stops the current playback task and cancels any active decoding work.
	/// </summary>
	private Task StopCoreAsync()
	{
		_pauseSignal.Set();
		return _playback.StopAsync();
	}

	[LoggerMessage(LogLevel.Error, Message = "Playback failed for track {TrackId} {TrackTitle}")]
	private partial void LogTrackError(string trackId, string? trackTitle, Exception exception);
}
