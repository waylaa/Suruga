using FFmpeg.AutoGen;
using Suruga.Audio.FFmpeg.Abstractions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg;

internal sealed unsafe class Packet : FFmpegResourceBase<AVPacket>
{
    internal int StreamIndex
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->stream_index;
        }
    }

    internal Packet()
    {
        Pointer = av_packet_alloc();

        if (Pointer is null)
        {
            throw new OutOfMemoryException();
        }
    }

    internal void Unreference()
    {
        if (!IsAlive)
        {
            return;
        }

        av_packet_unref(Pointer);
    }

    protected override void Release()
    {
        fixed (AVPacket** address = &Pointer)
        {
            av_packet_free(address);
        }
    }
}
