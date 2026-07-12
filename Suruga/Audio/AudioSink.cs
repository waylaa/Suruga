using NetCord.Gateway.Voice;
using Suruga.Primitives;

namespace Suruga.Audio;

/// <summary>
/// Represents an audio sink that writes decoded PCM samples to <see cref="OpusEncodeStream"/>.
/// </summary>
internal sealed class AudioSink : IAsyncDisposable
{
	/// <summary>
	/// Signaled when writable transport is available.
	/// Resets when the transport is detached or replaced (during reconnects).
	/// </summary>
	private readonly Signal _readySignal = new();

	/// <summary>
	/// Used to interrupt ongoing writes when the underlying transport changes.
	/// </summary>
	private readonly Interruption _transportInterruption = new();
	
	private readonly AsyncLock _lock = new();

	private OpusEncodeStream? _encodeStream;
	private bool _isDisposed;

	/// <summary>
	/// Attaches a new voice stream, replacing any existing one.
	/// </summary>
	/// <param name="voiceStream">The voice stream to write to.</param>
	/// <param name="token">A cancellation token.</param>
	internal async Task AttachAsync(Stream voiceStream, CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		OpusEncodeStream newEncodeStream = new(voiceStream, PcmFormat.Float, VoiceChannels.Stereo, OpusApplication.Audio);

		using (await _lock.EnterScopeAsync(token))
		{
			_readySignal.Reset();
			await _transportInterruption.InterruptAsync();
			
			OpusEncodeStream? oldEncodeStream = Interlocked.Exchange(ref _encodeStream, newEncodeStream);

			if (oldEncodeStream is not null)
			{
				try
				{
					await oldEncodeStream.DisposeAsync();
				}
				catch
				{
					// Ignore disposal exceptions to old streams.
				}
			}

			await _transportInterruption.RenewAsync();
			_readySignal.Set();
		}
	}

	/// <summary>
	/// Detaches the current transport. Future writes will block until new transport is attached.
	/// </summary>
	/// <param name="token">A cancellation token.</param>
	internal async Task DetachAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		using (await _lock.EnterScopeAsync(token))
		{
			_readySignal.Reset();
			await _transportInterruption.InterruptAsync();
			
			OpusEncodeStream? currentEncodeStream = Interlocked.Exchange(ref _encodeStream, null);

			if (currentEncodeStream is not null)
			{
				try
				{
					await currentEncodeStream.DisposeAsync();
				}
				catch
				{
					// Ignore disposal exceptions to the current stream.
				}
			}
		}
	}

	/// <summary>
	/// Writes PCM samples to the active <see cref="OpusEncodeStream"/>.
	/// </summary>
	/// <param name="pcm">The PCM audio samples to encode and send.</param>
	/// <param name="token">A cancellation token.</param>
	internal async ValueTask WriteAsync(ReadOnlyMemory<byte> pcm, CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);

		while (!token.IsCancellationRequested)
		{
			await _readySignal.WaitAsync(token: token);
			OpusEncodeStream? currentStream = Volatile.Read(ref _encodeStream);

			if (currentStream is null)
			{
				continue;
			}

            using CancellationTokenSource linkedCts = CancellationTokenSource
				.CreateLinkedTokenSource(token, _transportInterruption.Token);

			try
			{
				await currentStream.WriteAsync(pcm, linkedCts.Token);
				return;
			}
			catch (OperationCanceledException)
			{
				// Voice region server move or a reconnect happened mid-write.
				if (_transportInterruption.Token.IsCancellationRequested)
				{
					continue;
				}

				throw;
			}
		}
	}

	/// <summary>
	/// Flushes any buffered audio in <see cref="OpusEncodeStream"/>.
	/// </summary>
	/// <param name="token">A cancellation token.</param>
	internal Task FlushAsync(CancellationToken token = default)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		OpusEncodeStream? currentStream = Volatile.Read(ref _encodeStream);

		return currentStream is null ? Task.CompletedTask : currentStream.FlushAsync(token);
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
			await DetachAsync();
		}
		catch
		{
			// Ignore exceptions during disposal.
		}
		
		await _transportInterruption.DisposeAsync();
		_lock.Dispose();
	}
}
