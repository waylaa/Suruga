using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Suruga.Extensions;
using Suruga.Primitives;

namespace Suruga.Transport;

/// <summary>
/// A read-only audio byte stream that fetches audio data from YouTube adaptive formats.
/// </summary>
/// <remarks>
/// Implements chunked fetching with prefetching, automatic format rotation
/// on errors, and seek support.
/// </remarks>
internal sealed partial class YoutubeReadOnlyAudioByteStream : ReadOnlyAudioByteStream
{
    /// <summary>Gets the total length of the audio stream in bytes.</summary>
    /// <remarks>
	/// If the current format does not provide a content length,
	/// a HEAD request is issued once.
	/// </remarks>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override long Length => _currentFormat.ContentLength ?? (_fallbackLength ??= GetSegmentLength());

    /// <summary>Gets or sets the current position within the stream.</summary>
    /// <value>
    /// The position is automatically clamped between 0 and <see cref="Length"/>.
    /// </value>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override long Position
	{
		get;
		set => field = Math.Clamp(value, 0, Length);
	}
 
	private readonly HttpClient _client;
	private readonly IReadOnlyList<AdaptiveFormat> _formats;
    private readonly ILogger<YoutubeReadOnlyAudioByteStream> _logger;
 
	// FFmpeg reads from _activeBuffer while the next chunk is
	// being fetched into _prefetchBuffer in the background.
	private readonly IMemoryOwner<byte> _activeBufferOwner;
	private readonly IMemoryOwner<byte> _prefetchBufferOwner;

    private Task<int>? _prefetchTask;
    private Memory<byte> _activeBuffer;
	private Memory<byte> _prefetchBuffer;
	private bool _buffersDisposed;
    private long _prefetchChunkStart = -1;

    private AdaptiveFormat _currentFormat;
	private int _currentFormatIndex;
	private int _chunkBufferValidLength;
	private int _chunkBufferReadOffset;
	private long _currentChunkStart;
	private long _currentChunkEnd;

	private long? _fallbackLength;
	private int _consecutiveErrors;
 
	private const int MaxRetrievableChunkLength = 256 * 1024;

    /// <summary>
	/// Initializes a new instance of the <see cref="YoutubeReadOnlyAudioByteStream"/> class.
	/// </summary>
    /// <param name="client">The HTTP client used to send range and HEAD requests.</param>
    /// <param name="formats">A read‑only list of adaptive formats, ordered by
	/// priority (bitrate). The first format is used initially.
	/// </param>
    /// <param name="logger">Diagnostic logger.</param>
    internal YoutubeReadOnlyAudioByteStream
	(
		HttpClient client,
		IReadOnlyList<AdaptiveFormat> formats,
		ILogger<YoutubeReadOnlyAudioByteStream> logger
	)
	{
		_client = client;
		_formats = formats;
		_logger = logger;
		_currentFormat = formats[0];

		_activeBufferOwner = MemoryPool<byte>.Shared.Rent(MaxRetrievableChunkLength);
		_prefetchBufferOwner = MemoryPool<byte>.Shared.Rent(MaxRetrievableChunkLength);
		_activeBuffer = _activeBufferOwner.Memory;
		_prefetchBuffer = _prefetchBufferOwner.Memory;
 
		LogFormatType(_currentFormat.Codec);
	}

    /// <summary>
	/// Reads a span of bytes from the stream, fetching new chunks and rotating formats as needed.
	/// </summary>
    /// <param name="buffer">The span to write the read bytes into.</param>
    /// <returns>The number of bytes read, or 0 if the end of the stream is reached.</returns>
    /// <exception cref="InvalidOperationException">All formats and retries have been exhausted.</exception>
    public override int Read(Span<byte> buffer)
	{
		if (buffer.IsEmpty || Position >= Length)
		{
			return 0; // EOS.
		}
 
		for (int attempt = 0; attempt < 3; attempt++)
		{
			try
			{
				// If we've exhausted the current buffer or the position has drifted (seeked), fetch new data.
				if (_chunkBufferReadOffset >= _chunkBufferValidLength || Position < _currentChunkStart || Position > _currentChunkEnd)
				{
					ReadChunkData();
				}
 
				int availableBytesInChunk = _chunkBufferValidLength - _chunkBufferReadOffset;
				_consecutiveErrors = 0; // If we successfully reached this point, clear error counter.
 
				int bytesToRead = Math.Min(buffer.Length, availableBytesInChunk);
				_activeBuffer.Span.Slice(_chunkBufferReadOffset, bytesToRead).CopyTo(buffer);
 
				_chunkBufferReadOffset += bytesToRead;
				Position += bytesToRead;
 
				LogBuffering(Position.ToFormattedBytes(), Length.ToFormattedBytes());
				return bytesToRead;
			}
			catch (Exception ex)
			{
				_consecutiveErrors++;
				LogIoWarning(ex, ex.Message);
 
				// If we can't even get the first chunk after 3 retries, we're done.
				if (_consecutiveErrors >= 3 && _currentFormatIndex >= _formats.Count - 1)
				{
					throw new InvalidOperationException("Failed to read audio byte stream after exhausting all formats and retries.", ex);
				}
			}
		}
 
		return 0;
	}

    /// <summary>
	/// Sets the current position within the stream.
	/// </summary>
    /// <param name="offset">A byte offset relative to <paramref name="origin"/>.</param>
    /// <param name="origin">The reference point used to obtain the new position.</param>
    /// <returns>The new position, clamped between 0 and <see cref="Length"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="origin"/> is invalid.</exception>
    public override long Seek(long offset, SeekOrigin origin)
	{
		long newPosition = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => Position + offset,
			SeekOrigin.End => Length + offset,
			_ => throw new ArgumentOutOfRangeException(nameof(origin)),
		};
 
		Position = Math.Max(0, Math.Min(newPosition, Length));
		_chunkBufferValidLength = 0; // Force a buffer refresh on next Read() because the pointer moved.
 
		// If the seek lands outside the prefetched window, the in-flight fetch is useless.
		if (Position < _prefetchChunkStart || Position >= _prefetchChunkStart + MaxRetrievableChunkLength)
		{
			InvalidatePrefetch();
		}
 
		return Position;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && !_buffersDisposed)
		{
			_buffersDisposed = true;
			
			InvalidatePrefetch();
			_activeBufferOwner.Dispose();
			_prefetchBufferOwner.Dispose();
		}
		
		base.Dispose(disposing);
	}

    /// <summary>
    /// Reads the next chunk of data into the active buffer, utilizing the prefetch buffer
	/// if available, and initiates a background prefetch for the subsequent chunk.
    /// </summary>
    private void ReadChunkData()
	{
		// If we've failed 3 times on the current format, try the next one.
		if (_consecutiveErrors >= 3 && _currentFormatIndex + 1 < _formats.Count)
		{
			RotateFormat();
			_consecutiveErrors = 0;
			InvalidatePrefetch();
		}
 
		long chunkStart = Position;
		long chunkEnd = Math.Min(Position + MaxRetrievableChunkLength - 1, Length - 1);
		bool usedPrefetch = false;
 
		// The background task already fetched exactly this chunk.
		if (_prefetchTask is not null && _prefetchChunkStart == chunkStart)
		{
			try
			{
				int fetched = _prefetchTask.GetAwaiter().GetResult();
				_prefetchTask = null;
 
				// _prefetchBuffer becomes the new active buffer.
				(_activeBuffer, _prefetchBuffer) = (_prefetchBuffer, _activeBuffer);
				_chunkBufferValidLength = fetched;
				usedPrefetch = true;
			}
			catch
			{
				// Prefetch failed, discard and fall through to a synchronous fetch below.
				_prefetchTask = null;
				_prefetchChunkStart = -1;
			}
		}
 
		if (!usedPrefetch)
		{
			// Prefetch miss (first read, seek to an unexpected position, or prefetch error).
			InvalidatePrefetch();
			_chunkBufferValidLength = FetchIntoBuffer(chunkStart, chunkEnd, _activeBuffer, new Uri(_currentFormat.StreamUri));
		}
 
		_currentChunkStart = chunkStart;
		_currentChunkEnd = chunkEnd;
		_chunkBufferReadOffset = 0;
 
		// Start the next chunk in the background.
		long nextStart = chunkEnd + 1;

		if (nextStart < Length)
		{
			long nextEnd = Math.Min(nextStart + MaxRetrievableChunkLength - 1, Length - 1);
			_prefetchChunkStart = nextStart;
 
			// Capture locals before entering the Task, the parent fields may change
			// (format rotation, buffer swap) before the background thread runs.
			Memory<byte> bufferToFill = _prefetchBuffer;
			Uri streamUri = new(_currentFormat.StreamUri);
 
			_prefetchTask = Task.Run(() => FetchIntoBuffer(nextStart, nextEnd, bufferToFill, streamUri));
		}
	}

    /// <summary>
    /// Fetches a specific byte range from the stream URI and writes it into the provided buffer.
    /// </summary>
    /// <param name="chunkStart">The starting byte position of the chunk.</param>
    /// <param name="chunkEnd">The ending byte position of the chunk.</param>
    /// <param name="buffer">The memory buffer to store the fetched bytes.</param>
    /// <param name="streamUri">The URI of the stream to fetch from.</param>
    /// <returns>The number of bytes successfully fetched.</returns>
    private int FetchIntoBuffer(long chunkStart, long chunkEnd, Memory<byte> buffer, Uri streamUri)
	{
		int expectedBytes = (int)(chunkEnd - chunkStart + 1);
		
		using HttpRequestMessage request = new(HttpMethod.Get, streamUri);
		request.Headers.Range = new RangeHeaderValue(chunkStart, chunkEnd);
		
		using HttpResponseMessage response = _client.Send(request, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		
		using Stream networkStream = response.Content.ReadAsStream();
		networkStream.ReadExactly(buffer.Span[..expectedBytes]);
		
		return expectedBytes;
	}

    /// <summary>
    /// Cancels and waits for any ongoing prefetch task, then resets the prefetch state.
    /// </summary>
    private void InvalidatePrefetch()
	{
		if (_prefetchTask is not null)
		{
			try
			{
				_prefetchTask.Wait();
			}
			catch
			{
				// Ignore.
			}

			_prefetchTask = null;
		}
 
		_prefetchChunkStart = -1;
	}

    /// <summary>
    /// Advances to the next available adaptive format and resets the fallback length cache.
    /// </summary>
    private void RotateFormat()
	{
		_currentFormatIndex++;
		_currentFormat = _formats[_currentFormatIndex];
		_fallbackLength = null;
 
		LogFormatTypeSwitch(_currentFormat.Codec);
	}

    /// <summary>
    /// Issues an HTTP HEAD request to determine the total content length of the current format stream.
    /// </summary>
    /// <returns>The total length of the stream in bytes.</returns>
    /// <exception cref="InvalidOperationException">
	/// The content length could not be determined from the response headers.
	/// </exception>
    private long GetSegmentLength()
	{
		using HttpRequestMessage request = new(HttpMethod.Head, _currentFormat.StreamUri);
		using HttpResponseMessage response = _client.Send(request, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
 
		if (response.Content.Headers.ContentRange?.Length is long totalBytes)
		{
			return totalBytes;
		}
 
		if (response.Content.Headers.ContentLength is long contentLength && response.StatusCode is HttpStatusCode.OK)
		{
			return contentLength;
		}
 
		throw new InvalidOperationException("Could not determine content length for this audio byte stream.");
	}
 
	[LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
	private partial void LogIoWarning(Exception? exception, string? message);
 
	[LoggerMessage(Level = LogLevel.Warning, Message = "Switched audio track codec to '{codecType}' with lower bitrate. Expect reduced audio quality.")]
	private partial void LogFormatTypeSwitch(string codecType);
 
	[LoggerMessage(Level = LogLevel.Trace, Message = "Audio track codec is of type '{codecType}'.")]
	private partial void LogFormatType(string codecType);
 
	[LoggerMessage(Level = LogLevel.Trace, Message = "Buffering {start} of {total}.")]
	private partial void LogBuffering(string start, string total);
}
