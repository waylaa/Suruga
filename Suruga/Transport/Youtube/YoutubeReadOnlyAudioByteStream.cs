using Suruga.Common;
using Suruga.Resolvers.Primitives;
using Suruga.Transport.Extensions;

namespace Suruga.Transport.Youtube;

internal sealed class YoutubeReadOnlyAudioByteStream(HttpClient client, IReadOnlyList<AdaptiveFormat> formats) : ReadOnlyAudioByteStream
{
    public override long Length => _reader.GetLength();

    public override long Position
    {
        get;
        set => field = Math.Clamp(value, 0, Length);
    }

    private readonly YoutubeAudioChunkReader _reader = new(client, formats);

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty || Position >= Length)
        {
            return 0; // EOS.
        }

        int bytesRead = _reader.ReadAt(Position, Length, buffer);
        Position += bytesRead;
        
        Logger.Trace<YoutubeReadOnlyAudioByteStream>($"Buffering {Position.ToFormattedBytes()} of {Length.ToFormattedBytes()}.");
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
        
        Logger.Trace<YoutubeReadOnlyAudioByteStream>($"Seeking {newPosition.ToFormattedBytes()} of {Length.ToFormattedBytes()}.");

        Position = Math.Max(0, Math.Min(newPosition, Length));
        
        _reader.InvalidateChunk();
        _reader.InvalidateIfOutsidePrefetchWindow(Position);

        return Position;
    }
}
