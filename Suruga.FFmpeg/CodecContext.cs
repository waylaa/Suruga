using System.Runtime.CompilerServices;
using Suruga.FFmpeg.Extensions;
using Suruga.FFmpeg.Interop;
using Suruga.FFmpeg.Native;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg;

public sealed unsafe class CodecContext : FFmpegResource<AVCodecContext>
{
    public AVSampleFormat SampleFormat
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return Pointer->sample_fmt;
        }
    }

    public int SampleRate
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return Pointer->sample_rate;
        }
    }

    public CodecContext(StreamInfo streamInfo)
    {
        Pointer = NativeMethods.avcodec_alloc_context3(in streamInfo.Codec);

        if (!IsInitialized)
        {
            throw new OutOfMemoryException("Failed to allocate an AVCodecContext.");
        }
        
        // Fixes 'Could not update timestamps for skipped samples' warning.
        Pointer->pkt_timebase = streamInfo.Stream.Timebase;

        NativeMethods
            .avcodec_parameters_to_context(ref GetReference(), in streamInfo.Stream.CodecParameters)
            .ThrowIfError();

        AVDictionary* dictionary = null;
        NativeMethods.avcodec_open2(ref GetReference(), in streamInfo.Codec, ref dictionary).ThrowIfError();
    }
    
    public FFmpegResult SendPacket(Packet? packet)
    {
        ThrowIfDisposedOrUninitialized();
        ref readonly AVPacket @ref = ref packet is not null ? ref packet.GetReadonlyReference() : ref Unsafe.NullRef<AVPacket>();
        
        int returnCode = NativeMethods.avcodec_send_packet(ref GetReference(), in @ref)
            .ThrowIfError(rc => rc < 0 && rc != Constants.AVERROR_EOF && rc != Constants.Error(Constants.EAGAIN));

        return returnCode switch
        {
            Constants.AVERROR_EOF => FFmpegResult.EndOfStream,
            _ when returnCode == Constants.Error(Constants.EAGAIN) => FFmpegResult.NeedMoreInput,
            _ => FFmpegResult.Success
        };
    }

    public FFmpegResult ReceiveFrame(Frame frame)
    {
        ThrowIfDisposedOrUninitialized();
        
        int returnCode = NativeMethods.avcodec_receive_frame(ref GetReference(), ref frame.GetReference())
            .ThrowIfError(rc => rc < 0 && rc != Constants.AVERROR_EOF && rc != Constants.Error(Constants.EAGAIN));

        return returnCode switch
        {
            Constants.AVERROR_EOF => FFmpegResult.EndOfStream,
            _ when returnCode == Constants.Error(Constants.EAGAIN) => FFmpegResult.NeedMoreInput,
            _ => FFmpegResult.Success
        };
    }

    public void Flush()
    {
        ThrowIfDisposedOrUninitialized();
        NativeMethods.avcodec_flush_buffers(ref GetReference());
    }

    internal void GetChannelLayout(out AVChannelLayout destination)
    {
        ThrowIfDisposedOrUninitialized();

        destination = default;
        NativeMethods.av_channel_layout_copy(ref destination, in Pointer->ch_layout).ThrowIfError();
    }

    protected override void Release(ref AVCodecContext* addressOfPointer)
        => NativeMethods.avcodec_free_context(ref addressOfPointer);
}
