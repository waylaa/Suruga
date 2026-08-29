using Suruga.FFmpeg.Helpers;
using Suruga.FFmpeg.Native;

namespace Suruga.FFmpeg;

public sealed unsafe class MediaStream : FFmpegResource<AVStream>
{
    public ref readonly AVDictionary Metadata
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return ref PointerHelper.GetReadOnlyReference(Pointer->metadata);
        }
    }

    internal ref readonly AVCodecParameters CodecParameters
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return ref PointerHelper.GetReadOnlyReference(Pointer->codecpar);
        }
    }

    internal AVRational Timebase
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return Pointer->time_base;
        }
    }

    public MediaStream(AVStream* pStream)
    {
        ArgumentNullException.ThrowIfNull(pStream);
        Pointer = pStream;
    }
    
    protected override void Release(ref AVStream* addressOfPointer)
    {
        // AVStream lifetime is managed by AVFormatContext.
    }
}
