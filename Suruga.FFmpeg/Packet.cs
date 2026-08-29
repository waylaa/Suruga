using Suruga.FFmpeg.Interop;
using Suruga.FFmpeg.Native;

namespace Suruga.FFmpeg;

public sealed unsafe class Packet : FFmpegResource<AVPacket>
{
    public int StreamIndex
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return Pointer->stream_index;
        }
    }

    public Packet()
    {
        Pointer = NativeMethods.av_packet_alloc();

        if (!IsInitialized)
        {
            throw new OutOfMemoryException("Failed to allocate an AVPacket.");
        }
    }

    public void Unreference()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        
        if (!IsInitialized)
        {
            return;
        }
        
        NativeMethods.av_packet_unref(ref GetReference());
    }

    protected override void Release(ref AVPacket* addressOfPointer)
        => NativeMethods.av_packet_free(ref addressOfPointer);
}
