using Suruga.Common;
using Suruga.Resolvers.Primitives;
using Suruga.Transport.Policies;

namespace Suruga.Transport.Youtube;

internal sealed class YoutubeAudioChunkReader(HttpClient client, IReadOnlyList<AdaptiveFormat> formats)
{
    private readonly AdaptiveFormatFetcher _fetcher = new(client);
    private readonly ChunkPrefetcher _prefetcher = new(MaxRetrievableChunkLength);
    private readonly AdaptiveFormatRotator _rotator = new(formats);
    
    private int _chunkBufferReadOffset;
    private long? _fallbackLength;
    private int _consecutiveErrors;
    
    private const int MaxRetrievableChunkLength = 256 * 1024; // 256KB
    private const int MaxAttempts = 3;

    internal long GetLength()
        => _rotator.Current.ContentLength ?? (_fallbackLength ??= _fetcher.GetContentLength(_rotator.Current.StreamUri));
    
    internal int ReadAt(long position, long length, Span<byte> buffer)
    {
        int bytesToRead = InputOutputRetryPolicy.Execute
        (
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
                Logger.Warning<YoutubeAudioChunkReader>(ex);
            },
            (_, _) => _consecutiveErrors >= MaxAttempts && _rotator.IsLast,
            ex => throw new InvalidOperationException("Failed to read audio byte stream after exhausting all formats and retries.", ex)
        );

        _prefetcher.Buffer.Span.Slice(_chunkBufferReadOffset, bytesToRead).CopyTo(buffer);
        _chunkBufferReadOffset += bytesToRead;

        return bytesToRead;
    }

    internal void InvalidateChunk()
        => _prefetcher.InvalidateActiveChunk();

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
            
            Logger.Debug<YoutubeAudioChunkReader>($"Switched audio track codec to '{next.Codec}' with lower bitrate. Expect reduced audio quality.");
        }

        AdaptiveFormat format = _rotator.Current;
        _prefetcher.LoadChunk((start, end, buffer) => _fetcher.FetchRange(start, end, buffer, format.StreamUri), position, length);
        _chunkBufferReadOffset = 0;
    }
}
