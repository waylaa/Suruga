using Microsoft.Extensions.Logging;
using Suruga.Resolvers.Primitives;
using Suruga.Transport.Policies;

namespace Suruga.Transport.Youtube;

internal sealed partial class YoutubeAudioChunkReader
{
    private const int MaxRetrievableChunkLength = 256 * 1024;
    private const int MaxAttempts = 3;

    private readonly AdaptiveFormatFetcher _fetcher;
    private readonly ChunkPrefetcher _prefetcher = new(MaxRetrievableChunkLength);
    private readonly AdaptiveFormatRotator _rotator;
    private readonly ILogger _logger;

    private int _chunkBufferReadOffset;
    private long? _fallbackLength;
    private int _consecutiveErrors;

    internal YoutubeAudioChunkReader(HttpClient client, IReadOnlyList<AdaptiveFormat> formats, ILogger logger)
    {
        _fetcher = new AdaptiveFormatFetcher(client);
        _rotator = new AdaptiveFormatRotator(formats);
        _logger = logger;

        LogFormatType(_rotator.Current.Codec);
    }

    internal long GetLength()
        => _rotator.Current.ContentLength ?? (_fallbackLength ??= _fetcher.GetContentLength(_rotator.Current.StreamUri));

    /// <summary>
    /// Reads bytes starting at <paramref name="position"/>, fetching new chunks and
    /// rotating formats as needed. Mirrors <see cref="Stream.Read(Span{byte})"/> semantics.
    /// </summary>
    internal int ReadAt(long position, long length, Span<byte> buffer)
    {
        int bytesToRead = InputOutputRetryPolicy.Execute(
            MaxAttempts,
            buffer,
            (_, buf) =>
            {
                if (_chunkBufferReadOffset >= _prefetcher.ValidLength ||
                    position < _prefetcher.ChunkStart ||
                    position > _prefetcher.ChunkEnd)
                {
                    LoadChunk(position, length);
                }

                int availableBytesInChunk = _prefetcher.ValidLength - _chunkBufferReadOffset;
                _consecutiveErrors = 0;

                return Math.Min(buf.Length, availableBytesInChunk);
            },
            _ => true,
            (ex, _) =>
            {
                _consecutiveErrors++;
                LogIoWarning(ex, ex.Message);
            },
            (_, _) => _consecutiveErrors >= MaxAttempts && _rotator.IsLast,
            ex => throw new InvalidOperationException("Failed to read audio byte stream after exhausting all formats and retries.", ex));

        _prefetcher.Buffer.Span.Slice(_chunkBufferReadOffset, bytesToRead).CopyTo(buffer);
        _chunkBufferReadOffset += bytesToRead;

        return bytesToRead;
    }

    internal void InvalidateChunk() => _prefetcher.InvalidateActiveChunk();

    internal void InvalidateIfOutsidePrefetchWindow(long position)
    {
        if (_prefetcher.IsOutsidePrefetchWindow(position))
        {
            _prefetcher.Invalidate();
        }
    }

    private void LoadChunk(long position, long length)
    {
        if (_consecutiveErrors >= MaxAttempts && !_rotator.IsLast)
        {
            AdaptiveFormat next = _rotator.RotateToNext();
            _fallbackLength = null;
            _consecutiveErrors = 0;
            _prefetcher.Invalidate();

            LogFormatTypeSwitch(next.Codec);
        }

        AdaptiveFormat format = _rotator.Current;
        _prefetcher.LoadChunk((start, end, buffer) => _fetcher.FetchRange(start, end, buffer, format.StreamUri), position, length);
        _chunkBufferReadOffset = 0;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    private partial void LogIoWarning(Exception? exception, string? message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Switched audio track codec to '{codecType}' with lower bitrate. Expect reduced audio quality.")]
    private partial void LogFormatTypeSwitch(string codecType);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Audio track codec is of type '{codecType}'.")]
    private partial void LogFormatType(string codecType);
}
