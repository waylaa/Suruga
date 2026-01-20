using FFmpeg.AutoGen;
using Suruga.Audio.FFmpeg.Abstractions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg;

internal sealed unsafe class Frame : FFmpegResourceBase<AVFrame>
{
    internal int SampleCountPerChannel
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->nb_samples;
        }
    }

    internal Frame()
    {
        Pointer = av_frame_alloc();

        if (Pointer is null)
        {
            throw new OutOfMemoryException();
        }
    }

    internal DecodedFrame<float> ToDecodedFrame()
    {
        int channels = Pointer->ch_layout.nb_channels;
        int samplesPerChannel = SampleCountPerChannel;
        int totalSamples = samplesPerChannel * channels;

        DecodedFrame<float> frame = new(totalSamples);
        using DecodedFrame<float>.PinnedDecodedFrame pinnedFrame = frame.Pin();

        pinnedFrame.WithPinnedSpan(totalSamples, (destinationBuffer, totalSamples) =>
        {
            ReadOnlySpan<float> audioData = new(Pointer->extended_data[0], totalSamples);
            audioData.CopyTo(destinationBuffer);
        });

        return frame;
    }

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
        fixed (AVFrame** address = &Pointer)
        {
            av_frame_free(address);
        }
    }
}
