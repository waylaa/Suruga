using FFmpeg.AutoGen;
using Suruga.Audio.FFmpeg.Abstractions;
using Suruga.Audio.FFmpeg.Extensions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.FFmpeg;

internal sealed unsafe class FormatContext : FFmpegResourceBase<AVFormatContext>
{
    internal readonly AVStream* StreamPtr;
    internal readonly AVCodec* CodecPtr;

    internal FormatContext(ByteStreamContext byteStreamContext)
    {
        Pointer = avformat_alloc_context();

        if (Pointer is null)
        {
            throw new OutOfMemoryException();
        }

        Pointer->pb = byteStreamContext.Pointer;
        Pointer->flags |= AVFMT_FLAG_CUSTOM_IO;

        fixed (AVFormatContext** formatContextAddress = &Pointer)
        {
            // error -5 here.
            int res = avformat_open_input(formatContextAddress, url: null, fmt: null, options: null);
            int dbg = 0;
        }

        avformat_find_stream_info(Pointer, options: null).ThrowOnError();

        fixed (AVCodec** codecAddress = &CodecPtr)
        {
            int streamIndex = av_find_best_stream
            (
                ic: Pointer,
                type: AVMediaType.AVMEDIA_TYPE_AUDIO,
                wanted_stream_nb: -1,
                related_stream: -1,
                decoder_ret: codecAddress,
                flags: 0
            );

            streamIndex.ThrowOnError();
            StreamPtr = Pointer->streams[streamIndex];
        }
    }
    
    internal CodecContext CreateCodecContext()
        => new(StreamPtr, CodecPtr);

    internal FFmpegStatus ReadFrame(Packet packet)
    {
        int returnCode = av_read_frame(Pointer, packet.Pointer);

        if (returnCode == AVERROR_EOF)
        {
            return FFmpegStatus.EndOfStream;
        }

        if (returnCode == ByteStreamContext.AverrorEio)
        {
            return FFmpegStatus.ByteStreamError;
        }

        if (packet.StreamIndex != StreamPtr->index)
        {
            return FFmpegStatus.Discard;
        }

        returnCode.ThrowOnError();
        return FFmpegStatus.Success;
    }

    protected override void Release()
    {
        fixed (AVFormatContext** address = &Pointer)
        {
            avformat_close_input(address);
        }
    }
}
