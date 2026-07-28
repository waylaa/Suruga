using NetCord.Gateway.Voice;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Audio;

/// <summary>
/// Represents an audio sink that writes decoded PCM samples to <see cref="OpusEncodeStream"/>.
/// </summary>
internal sealed class AudioSink : VolatileAsyncDisposable
{
	private readonly VolatileSwappableAsyncDisposableResource<OpusEncodeStream> _opusEncodeStreamResource = new();

	/// <summary>
	/// Attaches a new voice stream, replacing any existing one.
	/// </summary>
	/// <param name="voiceStream">The voice stream to write to.</param>
	/// <param name="token">A cancellation token.</param>
	internal Task AttachAsync(Stream voiceStream, CancellationToken token = default)
	{
		ThrowIfDisposed();
		
		OpusEncodeStream newEncodeStream = new(voiceStream, PcmFormat.Float, VoiceChannels.Stereo, OpusApplication.Audio);
		return _opusEncodeStreamResource.AttachAsync(newEncodeStream, token);
	}

	/// <summary>
	/// Detaches the current transport. Future writes will block until new transport is attached.
	/// </summary>
	/// <param name="token">A cancellation token.</param>
	internal Task DetachAsync(CancellationToken token = default)
	{
		ThrowIfDisposed();
		return _opusEncodeStreamResource.DetachAsync(token);
	}

	/// <summary>
	/// Writes PCM samples to the active <see cref="OpusEncodeStream"/>.
	/// </summary>
	/// <param name="pcm">The PCM audio samples to encode and send.</param>
	/// <param name="token">A cancellation token.</param>
	internal ValueTask WriteAsync(ReadOnlyMemory<byte> pcm, CancellationToken token = default)
	{
		ThrowIfDisposed();
		return _opusEncodeStreamResource.UseAsync(async (stream, ct) => await stream.WriteAsync(pcm, ct), token);
	}

	/// <summary>
	/// Flushes any buffered audio in <see cref="OpusEncodeStream"/>.
	/// </summary>
	/// <param name="token">A cancellation token.</param>
	internal Task FlushAsync(CancellationToken token = default)
	{
		ThrowIfDisposed();
		return _opusEncodeStreamResource.Peek() is OpusEncodeStream encodeStream ? encodeStream.FlushAsync(token) : Task.CompletedTask;
	}

	protected override ValueTask DisposeCoreAsync()
		=> _opusEncodeStreamResource.DisposeAsync();
}
