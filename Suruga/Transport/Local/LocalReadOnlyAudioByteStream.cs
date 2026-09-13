using Microsoft.Win32.SafeHandles;
using Suruga.Common;
using Suruga.Transport.Extensions;
using Suruga.Transport.Policies;

namespace Suruga.Transport.Local;

internal sealed class LocalReadOnlyAudioByteStream : ReadOnlyAudioByteStream
{
    public override long Length { get; }
    
    public override long Position
    {
        get;
        set => field = Math.Clamp(value, 0, Length);
    }

    private readonly SafeFileHandle _fileHandle;
    
    internal LocalReadOnlyAudioByteStream(string streamUri)
    {
        _fileHandle = File.OpenHandle(streamUri);
        Length = RandomAccess.GetLength(_fileHandle);
    }
    
    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty || Position >= Length)
        {
            return 0;
        }

        int bytesRead = InputOutputRetryPolicy.Execute
        (
            3,
            buffer,
            (_, buf) => RandomAccess.Read(_fileHandle, buf, Position),
            ex => ex is IOException,
            (ex, _) => Logger.Warning<LocalReadOnlyAudioByteStream>(ex),
            (_, attempt) => attempt == 2,
            ex => new InvalidOperationException("Failed to read from local file after exhausting all retries.", ex)
        );

        Position += bytesRead;
        Logger.Trace<LocalReadOnlyAudioByteStream>($"Buffering {Position.ToFormattedBytes()} of {Length.ToFormattedBytes()}.");
        
        return bytesRead;
    }

    /// <summary>
    /// Sets the current position within the file stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">
    /// A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.
    /// </param>
    /// <returns>The new position within the stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="origin"/> is not a valid <see cref="SeekOrigin"/> value.
    /// </exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        
        Logger.Trace<LocalReadOnlyAudioByteStream>($"Seeking {newPosition.ToFormattedBytes()} of {Length.ToFormattedBytes()}.");
        return Position = Math.Max(0, Math.Min(newPosition, Length));
    } 

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileHandle.Dispose();
        }

        base.Dispose(disposing);
    }
}
