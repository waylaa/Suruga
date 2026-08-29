using Suruga.FFmpeg.Interop;
using Suruga.FFmpeg.Native;

namespace Suruga.FFmpeg;

public sealed unsafe class Frame : FFmpegResource<AVFrame>
{
    public ref byte* ExtendedData
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return ref Pointer->extended_data[0];
        }
    }
    
    public int SamplesPerChannel
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return Pointer->nb_samples;
        }
    }

    public Frame()
    {
        Pointer = NativeMethods.av_frame_alloc();

        if (!IsInitialized)
        {
            throw new OutOfMemoryException("Failed to allocate an AVFrame.");
        }
    }

    public void Unreference()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        
        if (!IsInitialized)
        {
            return;
        }

        NativeMethods.av_frame_unref(ref GetReference());
    }

    protected override void Release(ref AVFrame* addressOfPointer)
        => NativeMethods.av_frame_free(ref addressOfPointer);
}
