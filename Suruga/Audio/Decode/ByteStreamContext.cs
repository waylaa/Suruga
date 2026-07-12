using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Suruga.Transport;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.Decode;

/// <summary>
/// A wrapper for <see cref="AVIOContext"/> used for reading audio data from
/// a <see cref="ReadOnlyAudioByteStream"/>.
/// </summary>
internal sealed unsafe class ByteStreamContext : FFmpegResource<AVIOContext>
{
    // avio_alloc_context requires stable function pointers for reading and seeking.
    // Store both in these static fields so the instances to ReadPacket and SeekPacket
    // will not get garbage collected.
    private static readonly avio_alloc_context_read_packet ReadPacketCallback = ReadPacket;
    private static readonly avio_alloc_context_seek SeekCallback = Seek;
    
    private readonly ReadOnlyAudioByteStream _byteStream;

    private GCHandle _selfHandle;
    private byte* _pBuffer;

    /// <summary>
    /// Gets the FFmpeg I/O error code used when stream operations fail.
    /// </summary>
    internal const int AverrorEio = -5;

    private const int BufferSize = 32 * 1024; // 32KB
    private const int SeekSet = 0;
    private const int SeekCur = 1;
    private const int SeekEnd = 2;

    /// <summary>
    /// Initializes a byte stream context for the specified audio stream.
    /// </summary>
    /// <param name="byteStream">The stream used to supply media data.</param>
    internal ByteStreamContext(ReadOnlyAudioByteStream byteStream)
    {
        _byteStream = byteStream;
        _selfHandle = GCHandle.Alloc(this);
        _pBuffer = (byte*)av_malloc(BufferSize);
        
        Pointer = avio_alloc_context
        (
            _pBuffer,
            BufferSize,
            0, // Disable writing.
            GCHandle.ToIntPtr(_selfHandle).ToPointer(),
            ReadPacketCallback,
            null,
            SeekCallback
        );
        
        if (!IsValid)
        {
            av_free(_pBuffer);
            _selfHandle.Free();
            
            throw new OutOfMemoryException();
        }
    }

    protected override void Release()
    {
        fixed (AVIOContext** ppIoContext = &Pointer)
        {
            avio_context_free(ppIoContext); // _bufferPtr is automatically freed.
        }

        _pBuffer = null;

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
    }

    private static int ReadPacket(void* opaque, byte* buf, int bufSize)
    {
        try
        {
            if (GCHandle.FromIntPtr((nint)opaque).Target is not ByteStreamContext { IsAlive: true } context)
            {
                return AverrorEio;
            }

            Span<byte> destination = new(buf, bufSize);
            int bytesRead = context._byteStream.Read(destination);

            return bytesRead switch
            {
                > 0 => bytesRead,
                0 => AVERROR_EOF,
                _ => AVERROR(AverrorEio)
            };
        }
        catch
        {
            return AverrorEio;
        }
    }

    private static long Seek(void* opaque, long offset, int whence)
    {
        try
        {
            if (GCHandle.FromIntPtr((nint)opaque).Target is not ByteStreamContext context || !context.IsAlive)
            {
                return AverrorEio;
            }

            // Size query.
            if ((whence & AVSEEK_SIZE) != 0)
            {
                return context._byteStream.Length;
            }
            
            SeekOrigin origin = whence switch
            {
                SeekSet => SeekOrigin.Begin,
                SeekCur => SeekOrigin.Current,
                SeekEnd => SeekOrigin.End,
                _ => (SeekOrigin)(-1)
            };

            if ((int)origin == -1)
            {
                return -1;
            }

            long newPosition = context._byteStream.Seek(offset, origin);

            if (newPosition < 0)
            {
                return -1;
            }

            return newPosition;
        }
        catch
        {
            return -1;
        }
    }
}
