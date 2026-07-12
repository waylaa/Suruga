using FFmpeg.AutoGen;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.Decode;

/// <summary>
/// A wrapper for <see cref="AVPacket"/> used to store encoded media data.
/// </summary>
internal sealed unsafe class Packet : FFmpegResource<AVPacket>
{
    /// <summary>
    /// Gets the index of the stream associated with this packet.
    /// </summary>
    internal int StreamIndex
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->stream_index;
        }
    }

    /// <summary>
    /// Initializes a new packet instance.
    /// </summary>
    internal Packet()
    {
        Pointer = av_packet_alloc();

        if (!IsValid)
        {
            throw new OutOfMemoryException();
        }
    }

    /// <summary>
    /// Releases the packet's referenced data while retaining the packet itself.
    /// </summary>
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
        fixed (AVPacket** ppPacket = &Pointer)
        {
            av_packet_free(ppPacket);
        }
    }
}
