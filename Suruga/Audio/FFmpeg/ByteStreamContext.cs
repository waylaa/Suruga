using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Suruga.Audio.FFmpeg.Abstractions;
using Suruga.Transport;
using Suruga.Transport.Abstractions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg;

internal sealed unsafe class ByteStreamContext : FFmpegResourceBase<AVIOContext>
{
    private readonly IAudioByteStream _byteStream;
    private readonly avio_alloc_context_read_packet _readCallback;
    private readonly avio_alloc_context_seek _seekCallback;
    
    private GCHandle _selfHandle;
    private byte* _bufferPtr;

    internal const int AverrorEio = -5;
    private const int BufferSize =  64 * 1024; // 64KB
    private const int AvseekSize = 0x10000;
    private const int SeekSet = 0;
    private const int SeekCur = 1;
    private const int SeekEnd = 2;

    internal ByteStreamContext(IAudioByteStream byteStream)
    {
        _byteStream = byteStream;
        _readCallback = ReadPacket;
        _seekCallback = Seek;
        _selfHandle = GCHandle.Alloc(this);
        _bufferPtr = (byte*)av_malloc(BufferSize);

        Pointer = avio_alloc_context
        (
            buffer: _bufferPtr,
            buffer_size: BufferSize,
            write_flag: 0, // Disable writing.
            opaque: GCHandle.ToIntPtr(_selfHandle).ToPointer(),
            read_packet: _readCallback,
            write_packet: null,
            seek: _seekCallback
        );

        if (Pointer is null)
        {
            av_free(_bufferPtr);
            _selfHandle.Free();

            throw new OutOfMemoryException();
        }

        if (byteStream is YoutubeAudioByteStream)
        {
            Pointer->seekable = 0; // Disable seeking on youtube byte streams.
        }
    }

    protected override void Release()
    {
        fixed (AVIOContext** address = &Pointer)
        {
            // _bufferPtr is automatically freed.
            avio_context_free(address);
        }

        _bufferPtr = null;

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
    }

    private static int ReadPacket(void* opaque, byte* buf, int bufSize)
    {
        try
        {
            if (GCHandle.FromIntPtr((nint)opaque).Target is not ByteStreamContext context || context.IsDisposed)
            {
                return AverrorEio;
            }

            Span<byte> destination = new(buf, bufSize);
            int bytesRead = context._byteStream.Read(destination);

            return bytesRead switch
            {
                > 0 => bytesRead,
                0 => AVERROR_EOF,
                _ => AVERROR(EAGAIN)
            };

            // return bytesRead == 0 ? AVERROR_EOF : bytesRead;
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
            if (GCHandle.FromIntPtr((nint)opaque).Target is not ByteStreamContext context || context.IsDisposed)
            {
                return AverrorEio;
            }

            // FFmpeg size query.
            if ((whence & AvseekSize) != 0)
            {
                return -1;
            }

            return whence switch
            {
                SeekSet => context._byteStream.Seek(offset, SeekOrigin.Begin),
                SeekCur => context._byteStream.Seek(offset, SeekOrigin.Current),
                SeekEnd => -1,// Streaming, can't seek to end.
                _ => -1,
            };
        }
        catch
        {
            return -1;
        }
    }
}
