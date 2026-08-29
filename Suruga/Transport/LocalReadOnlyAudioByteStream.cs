using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Suruga.Transport.Extensions;

namespace Suruga.Transport;

/// <summary>
/// A read-only audio byte stream that reads data directly from a local file
/// using <see cref="RandomAccess"/>.
/// </summary>
/// <remarks>
/// Supports seeking and includes automatic retry logic (up to three attempts)
/// for transient I/O failures.
/// </remarks>
internal sealed partial class LocalReadOnlyAudioByteStream : ReadOnlyAudioByteStream
{
    /// <summary>Gets the total length of the audio file in bytes.</summary>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override long Length { get; }

    /// <summary>Gets or sets the current position within the file stream.</summary>
    /// <value>
    /// The position is automatically clamped between 0 and <see cref="Length"/>.
    /// </value>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override long Position
    {
        get;
        set => field = Math.Clamp(value, 0, Length);
    }

    private readonly ILogger<LocalReadOnlyAudioByteStream> _logger;
    private readonly SafeFileHandle _fileHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalReadOnlyAudioByteStream"/> class.
    /// </summary>
    /// <param name="streamUri">Absolute file path to the audio file.</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="streamUri"/> or <paramref name="logger"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Read permission is denied.</exception>
    /// <exception cref="IOException">An I/O error occurred while opening the file.</exception>
    internal LocalReadOnlyAudioByteStream(string streamUri, ILogger<LocalReadOnlyAudioByteStream> logger)
    {
        _logger = logger;
        _fileHandle = File.OpenHandle(streamUri);
        Length = RandomAccess.GetLength(_fileHandle);
    }

    /// <summary>
    /// Reads a sequence of bytes from the current file stream and advances the position
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
    /// <exception cref="InvalidOperationException">
    /// An I/O error occurred and all retry attempts have been exhausted.
    /// </exception>
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
                int bytesRead = RandomAccess.Read(_fileHandle, buffer, Position);
                Position += bytesRead;
                
                LogBuffering(Position.ToFormattedBytes(), Length.ToFormattedBytes());
                return bytesRead;
            }
            catch (IOException ex)
            {
                LogIoWarning(ex, ex.Message);

                if (attempt == 2)
                {
                    throw new InvalidOperationException("Failed to read from local file after exhausting all retries.", ex);
                }
            }
        }

        return 0;
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
    
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    private partial void LogIoWarning(Exception? exception, string? message);
    
    [LoggerMessage(Level = LogLevel.Trace, Message = "Buffering {start} of {total}.")]
    private partial void LogBuffering(string start, string total);
}
