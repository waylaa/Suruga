using FFmpeg.AutoGen;
using Suruga.Audio.FFmpeg.Abstractions;
using Suruga.Audio.FFmpeg.Extensions;
using Suruga.Primitives;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg;

internal sealed unsafe class CodecContext : FFmpegResourceBase<AVCodecContext>
{
    internal AVChannelLayout ChannelLayout
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->ch_layout;
        }
    }

    internal AVSampleFormat SampleFormat
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->sample_fmt;
        }
    }

    internal int SampleRate
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->sample_rate;
        }
    }

    internal CodecContext(AVStream* streamPtr, AVCodec* codecPtr)
    {
        Pointer = avcodec_alloc_context3(codecPtr);

        if (Pointer is null)
        {
            throw new OutOfMemoryException();
        }

        // Fixes 'Could not update timestamps for skipped samples.' warning.
        Pointer->pkt_timebase = streamPtr->time_base;

        avcodec_parameters_to_context(Pointer, streamPtr->codecpar).ThrowOnError();
        avcodec_open2(Pointer, codecPtr, options: null).ThrowOnError();
    }

    internal FilterGraph CreateFilterGraph(AVSampleFormat outputFormat, int outputSampleRate, int outputChannels)
    {
        AVChannelLayout outputLayout = default;
        av_channel_layout_default(&outputLayout, outputChannels);
        
        return new FilterGraph
        (
            ChannelLayout,
            SampleFormat,
            SampleRate,
            outputLayout,
            outputFormat,
            outputSampleRate
        );
    }

    internal Packet CreatePacket()
        => new Packet();
    
    internal Frame CreateFrame()
        => new Frame();
    
    /// <summary>
    /// Sends raw data from a packet to the decoder.
    /// If <paramref name="packet"/> is null, the decoder flushes instead.
    /// </summary>
    /// <param name="packet">The packet to be sent to the decoder or null to flush.</param>
    /// <returns>[<see cref="int"/>] A response value.</returns>
    internal FFmpegStatus SendPacket(Packet? packet)
    {
        int returnCode = avcodec_send_packet(Pointer, packet is not null ? packet.Pointer : null);

        if (returnCode == AVERROR_EOF)
        {
            return FFmpegStatus.EndOfStream;
        }

        if (returnCode == AVERROR(EAGAIN))
        {
            return FFmpegStatus.NeedMoreInput;
        }

        returnCode.ThrowOnError();
        return FFmpegStatus.Success;
    }

    internal FFmpegStatus ReceiveFrame(Frame frame)
    {
        int returnCode = avcodec_receive_frame(Pointer, frame.Pointer);

        if (returnCode == AVERROR_EOF)
        {
            return FFmpegStatus.EndOfStream;
        }

        if (returnCode == AVERROR(EAGAIN))
        {
            return FFmpegStatus.NeedMoreInput;
        }

        returnCode.ThrowOnError();
        return FFmpegStatus.Success;
    }

    protected override void Release()
    {
        fixed (AVCodecContext** address = &Pointer)
        {
            avcodec_free_context(address);
        }
    }
}
