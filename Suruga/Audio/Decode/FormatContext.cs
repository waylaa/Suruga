using FFmpeg.AutoGen;
using Suruga.Audio.Decode.Primitives;
using Suruga.Extensions;
using static FFmpeg.AutoGen.ffmpeg;

namespace Suruga.Audio.Decode;

/// <summary>
/// A wrapper for <see cref="AVFormatContext"/> used for reading media containers.
/// </summary>
internal sealed unsafe class FormatContext : FFmpegResource<AVFormatContext>
{
    private int _currentStreamIndex = -1;

    /// <summary>
    /// Initializes a format context that reads from a custom byte stream.
    /// </summary>
    /// <param name="context">The byte stream context to read from.</param>
    internal FormatContext(ByteStreamContext context)
    {
        Pointer = avformat_alloc_context();

        if (!IsValid)
        {
            throw new OutOfMemoryException();
        }

        Pointer->pb = context.Pointer;
        Pointer->flags |= AVFMT_FLAG_CUSTOM_IO;
        
        fixed (AVFormatContext** ppFormatContext = &Pointer)
        {
            avformat_open_input(ppFormatContext, null, null, null).ThrowOnError(rc => rc < 0);
        }
    }

    /// <summary>
    /// Initializes a format context that reads from the specified URI.
    /// </summary>
    /// <param name="uri">The media URI or file path.</param>
    internal FormatContext(string uri)
    {
        Pointer = avformat_alloc_context();
        
        if (!IsValid)
        {
            throw new OutOfMemoryException();
        }
        
        fixed (AVFormatContext** ppFormatContext = &Pointer)
        {
            avformat_open_input(ppFormatContext, uri, null, null).ThrowOnError(rc => rc < 0);
        }
    }

    /// <summary>
    /// Locates the best audio stream and its associated codec.
    /// </summary>
    /// <param name="pCodec">
    /// When this method returns, contains a pointer to the selected codec.
    /// </param>
    /// <param name="pStream">
    /// When this method returns, contains a pointer to the selected stream.
    /// </param>
    internal void GetStream(out AVCodec* pCodec, out AVStream* pStream)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        avformat_find_stream_info(Pointer, null).ThrowOnError(rc => rc < 0);

        fixed (AVCodec** ppCodec = &pCodec)
        {
            _currentStreamIndex = av_find_best_stream
            (
                Pointer,
                AVMediaType.AVMEDIA_TYPE_AUDIO,
                -1,
                -1,
                ppCodec,
                0

            ).ThrowOnError(rc => rc < 0);

            pStream = Pointer->streams[_currentStreamIndex];
        }
    }

    
    /// <summary>
    /// Reads the next packet from the media source.
    /// </summary>
    /// <param name="packet">The packet that receives the decoded data.</param>
    /// <returns>
    /// A value indicating whether a packet was read successfully,
    /// discarded, or if an end-of-stream or read condition occurred.
    /// </returns>
    internal FFmpegResponse ReadFrame(Packet packet)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);
        
        int returnCode = av_read_frame(Pointer, packet.Pointer)
            .ThrowOnError(rc => rc < 0 && rc != AVERROR_EOF && rc != ByteStreamContext.AverrorEio);
        
        return returnCode switch
        {
            _ when returnCode == AVERROR_EOF => FFmpegResponse.EndOfStream,
            ByteStreamContext.AverrorEio => FFmpegResponse.ByteStreamError,
            _ when packet.StreamIndex != _currentStreamIndex => FFmpegResponse.Discard,
            _ => FFmpegResponse.Success
        };
    }

    /// <summary>
    /// Seeks to the specified position within the selected stream.
    /// </summary>
    /// <param name="timestamp">The target playback position.</param>
    internal void SeekFrame(TimeSpan timestamp)
    {
        ObjectDisposedException.ThrowIf(!IsAlive, this);

        if (_currentStreamIndex == -1)
        {
            throw new InvalidOperationException("Cannot seek without a selected audio stream.");
        }
        
        AVStream* pStream = Pointer->streams[_currentStreamIndex];
        
        long timestampTicks = av_rescale_q
        (
            timestamp.Ticks,
            new AVRational { num = 1, den = (int)TimeSpan.TicksPerSecond },
            pStream->time_base
        );
        
        timestampTicks = Math.Max(0, timestampTicks);
        long durationTicks = pStream->duration;

        if (durationTicks > 0)
        {
            timestampTicks = Math.Min(timestampTicks, durationTicks - 1);
        }
        
        av_seek_frame(Pointer, _currentStreamIndex, timestampTicks, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_ANY);
    }

    protected override void Release()
    {
        fixed (AVFormatContext** ppFormatContext = &Pointer)
        {
            avformat_close_input(ppFormatContext);
        }
    }
}
