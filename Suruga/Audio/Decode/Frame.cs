using FFmpeg.AutoGen;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.Decode;

/// <summary>
/// A wrapper for <see cref="AVFrame"/> used to store decoded audio data.
/// </summary>
internal sealed unsafe class Frame : FFmpegResource<AVFrame>
{
    /// <summary>
    /// Gets the number of samples contained in each channel.
    /// </summary>
    internal int SampleCountPerChannel
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->nb_samples;
        }
    }

    /// <summary>
    /// Gets the pointers to the frame's audio data buffers.
    /// </summary>
    internal byte** ExtendedData
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->extended_data;
        }
    }

    /// <summary>
    /// Initializes a new frame instance.
    /// </summary>
    internal Frame()
    {
        Pointer = av_frame_alloc();

        if (!IsValid)
        {
            throw new OutOfMemoryException();
        }
    }

    /// <summary>
    /// Releases the frame's referenced data while retaining the frame itself.
    /// </summary>
    internal void Unreference()
    {
        if (!IsAlive)
        {
            return;
        }

        av_frame_unref(Pointer);
    }

    protected override void Release()
    {
        fixed (AVFrame** ppFrame = &Pointer)
        {
            av_frame_free(ppFrame);
        }
    }
}
