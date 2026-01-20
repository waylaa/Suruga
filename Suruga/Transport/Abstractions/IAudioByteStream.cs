using Suruga.Primitives;

namespace Suruga.Transport.Abstractions;

/// <summary>
/// Byte-level audio streaming.
/// </summary>
internal interface IAudioByteStream : IDisposable
{
    /// <summary>
    /// Optional total length of the stream, or null if unknown (e.g., YouTube livestreams)
    /// </summary>
    long? Length { get; }

    /// <summary>
    /// Reads up to <paramref name="destination.Length"/> bytes into <paramref name="destination"/>.
    /// Returns the number of bytes read, or 0 if EOF.
    /// </summary>
    int Read(Span<byte> destination);

    /// <summary>
    /// Seeks to the given offset.
    /// Returns the new absolute position, or -1 if unsupported.
    /// </summary>
    long Seek(long offset, SeekOrigin origin);
    

}
