using Microsoft.Extensions.Logging;
using Suruga.Resolvers.Primitives;
using Suruga.Transport.Extensions;

namespace Suruga.Transport.Youtube;

internal sealed partial class YoutubeReadOnlyAudioByteStream : ReadOnlyAudioByteStream
{
    public override long Length => _reader.GetLength();

    public override long Position
    {
        get;
        set => field = Math.Clamp(value, 0, Length);
    }

    private readonly YoutubeAudioChunkReader _reader;
    private readonly ILogger<YoutubeReadOnlyAudioByteStream> _logger;

    internal YoutubeReadOnlyAudioByteStream(HttpClient client, IReadOnlyList<AdaptiveFormat> formats, ILogger<YoutubeReadOnlyAudioByteStream> logger)
    {
        _reader = new YoutubeAudioChunkReader(client, formats, logger);
        _logger = logger;
    }

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty || Position >= Length)
        {
            return 0; // EOS.
        }

        int bytesRead = _reader.ReadAt(Position, Length, buffer);
        Position += bytesRead;

        LogBuffering(Position.ToFormattedBytes(), Length.ToFormattedBytes());
        return bytesRead;
    }

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
        _reader.InvalidateChunk();
        _reader.InvalidateIfOutsidePrefetchWindow(Position);

        return Position;
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Buffering {start} of {total}.")]
    private partial void LogBuffering(string start, string total);
}
