namespace Suruga.Transport;

/// <summary>
/// Provides a base class for read-only byte-level audio streams that support
/// seeking and position tracking.
/// </summary>
public abstract class ReadOnlyAudioByteStream : Stream
{
    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// </summary>
    /// <value>Always returns <c>true</c>.</value>
    public sealed override bool CanRead => true;

    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// </summary>
    /// <value>Always returns <c>true</c>.</value>
    public sealed override bool CanSeek => true;

    /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// </summary>
    /// <value>Always returns <c>false</c>.</value>
    public sealed override bool CanWrite => false;

    /// <summary>
    /// Gets or sets the current position within the stream.
    /// </summary>
    /// <value>The current position within the stream.</value>
    public abstract override long Position { get; set; }

    /// <summary>
    /// Gets the total length of the stream in bytes.
    /// </summary>
    /// <value>The total length of the stream in bytes.</value>
    public abstract override long Length { get; }

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position 
    /// within the stream by the number of bytes read.
    /// </summary>
    /// <param name="buffer">
    /// A region of memory. When this method returns, the contents of this region 
    /// are replaced by the bytes read from the current source.
    /// </param>
    /// <returns>
    /// The total number of bytes read into the buffer. This can be less than the 
    /// number of bytes allocated in the buffer if that many bytes are not currently 
    /// available, or zero (<c>0</c>) if the end of the stream has been reached.
    /// </returns>
    public abstract override int Read(Span<byte> buffer);

    /// <summary>
    /// Sets the current position within the stream.
    /// </summary>
    /// <param name="offset">
    /// A byte offset relative to the <paramref name="origin"/> parameter.
    /// </param>
    /// <param name="origin">
    /// A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.
    /// </param>
    /// <returns>The new position within the stream.</returns>
    public abstract override long Seek(long offset, SeekOrigin origin);

    /// <summary>
    /// Not supported. Use <see cref="Read(Span{byte})"/> instead.
    /// </summary>
    /// <param name="buffer">An array of bytes.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data.</param>
    /// <param name="count">The maximum number of bytes to be read.</param>
    /// <returns>Never returns; always throws an exception.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public sealed override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public sealed override void SetLength(long value)
        => throw new NotSupportedException();

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <param name="buffer">An array of bytes.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin retrieving the data.</param>
    /// <param name="count">The number of bytes to be written.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public sealed override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public sealed override void Flush()
        => throw new NotSupportedException();
}
