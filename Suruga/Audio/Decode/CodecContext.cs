using FFmpeg.AutoGen;
using Suruga.Audio.Decode.Primitives;
using Suruga.Extensions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.Decode;

/// <summary>
/// A wrapper for <see cref="AVCodecContext"/> used for decoding audio streams.
/// </summary>
internal sealed unsafe class CodecContext : FFmpegResource<AVCodecContext>
{
    /// <summary>
    /// Gets the channel layout of the decoded audio.
    /// </summary>
    internal AVChannelLayout ChannelLayout
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->ch_layout;
        }
    }

    /// <summary>
    /// Gets the sample format produced by the decoder.
    /// </summary>
    internal AVSampleFormat SampleFormat
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->sample_fmt;
        }
    }

    /// <summary>
    /// Gets the sample rate of the decoded audio.
    /// </summary>
    internal int SampleRate
    {
        get
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            return Pointer->sample_rate;
        }
    }

    /// <summary>
    /// Initializes a codec context for the specified codec and stream.
    /// </summary>
    /// <param name="pCodec">The codec to open.</param>
    /// <param name="pStream">The stream whose parameters are used to configure the codec.</param>
    internal CodecContext(AVCodec* pCodec, AVStream* pStream)
    {
        Pointer = avcodec_alloc_context3(pCodec);

        if (!IsValid)
        {
            throw new OutOfMemoryException();
        }

        // Fixes 'Could not update timestamps for skipped samples' warning.
        Pointer->pkt_timebase = pStream->time_base;
        
        avcodec_parameters_to_context(Pointer, pStream->codecpar).ThrowOnError(rc => rc < 0);
        avcodec_open2(Pointer, pCodec, null).ThrowOnError(rc => rc < 0);
    }

    /// <summary>
    /// Sends a packet to the decoder.
    /// </summary>
    /// <param name="packet">
    /// The packet to decode, or <see langword="null"/> to flush the decoder.
    /// </param>
    /// <returns>The result of the operation.</returns>
    internal FFmpegResponse SendPacket(Packet? packet)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);

        int returnCode = avcodec_send_packet(Pointer, packet is not null ? packet.Pointer : null)
            .ThrowOnError(rc => rc < 0 && rc != AVERROR_EOF && rc != AVERROR(EAGAIN) && rc != AVERROR_INVALIDDATA);

        return returnCode switch
        {
            _ when returnCode == AVERROR_EOF => FFmpegResponse.EndOfStream,
            _ when returnCode == AVERROR(EAGAIN) => FFmpegResponse.NeedMoreInput,
            _ when returnCode == AVERROR_INVALIDDATA => FFmpegResponse.InvalidData,
            _ => FFmpegResponse.Success
        };
    }

    /// <summary>
    /// Receives a decoded frame from the decoder.
    /// </summary>
    /// <param name="frame">The frame that receives the decoded audio data.</param>
    /// <returns>The result of the operation.</returns>
    internal FFmpegResponse ReceiveFrame(Frame frame)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);

        int returnCode = avcodec_receive_frame(Pointer, frame.Pointer)
            .ThrowOnError(rc => rc < 0 && rc != AVERROR_EOF && rc != AVERROR(EAGAIN));

        return returnCode switch
        {
            _ when returnCode == AVERROR_EOF => FFmpegResponse.EndOfStream,
            _ when returnCode == AVERROR(EAGAIN) => FFmpegResponse.NeedMoreInput,
            _ => FFmpegResponse.Success
        };
    }

    /// <summary>
    /// Flushes all internally buffered decoder data.
    /// </summary>
    internal void FlushBuffers()
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        avcodec_flush_buffers(Pointer);
    }

    protected override void Release()
    {
        fixed (AVCodecContext** ppCodecContext = &Pointer)
        {
            avcodec_free_context(ppCodecContext);
        }
    }
}
