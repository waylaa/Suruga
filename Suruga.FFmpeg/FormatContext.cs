using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Suruga.FFmpeg.Extensions;
using Suruga.FFmpeg.Helpers;
using Suruga.FFmpeg.Interop;
using Suruga.FFmpeg.Native;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg;

public sealed unsafe class FormatContext : FFmpegResource<AVFormatContext>
{
    public ref readonly AVDictionary Metadata
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return ref PointerHelper.GetReadOnlyReference(Pointer->metadata);
        }
    }
    
    public long Duration
    {
        get
        {
            ThrowIfDisposedOrUninitialized();
            return Pointer->duration;
        }
    }
    
    private int _selectedStreamIndex = -1;

    public FormatContext(InputOutputContext ioContext)
    {
        Pointer = NativeMethods.avformat_alloc_context();

        if (!IsInitialized)
        {
            throw new OutOfMemoryException("Failed to allocate an AVFormatContext.");
        }
        
        Pointer->pb = ioContext.Pointer;
        Pointer->flags |= Constants.AVFMT_FLAG_CUSTOM_IO;

        AVDictionary* pDictionary = null;
        NativeMethods.avformat_open_input(ref GetAddressOfPointer(), in Unsafe.NullRef<byte>(), in Unsafe.NullRef<AVInputFormat>(), ref pDictionary).ThrowIfError();
    }

    public FormatContext(string uri)
    {
        Pointer = NativeMethods.avformat_alloc_context();

        if (!IsInitialized)
        {
            throw new OutOfMemoryException("Failed to allocate an AVFormatContext.");
        }

        ReadOnlySpan<byte> utf8Uri = Encoding.UTF8.GetBytes(uri);

        AVDictionary* pDictionary = null;
        NativeMethods.avformat_open_input(ref GetAddressOfPointer(), in MemoryMarshal.GetReference(utf8Uri), in Unsafe.NullRef<AVInputFormat>(), ref pDictionary).ThrowIfError();
    }

    public StreamInfo GetStream()
    {
        ThrowIfDisposedOrUninitialized();
        AVDictionary* pDictionary = null;
        
        NativeMethods.avformat_find_stream_info(ref GetReference(), ref pDictionary).ThrowIfError();
        AVCodec* pCodec = null;
        
        _selectedStreamIndex = NativeMethods.av_find_best_stream
        (
            ref GetReference(),
            AVMediaType.AV_MEDIA_TYPE_AUDIO,
            -1,
            -1,
            ref pCodec,
            0
        ).ThrowIfError(rc => rc < 0);
        
        return new StreamInfo(new MediaStream(Pointer->streams[_selectedStreamIndex]), pCodec);
    }

    public void Seek(TimeSpan timestamp)
    {
        ThrowIfDisposedOrUninitialized();

        if (_selectedStreamIndex == -1)
        {
            throw new InvalidOperationException("Cannot seek without a selected audio stream.");
        }
        
        AVStream* pStream = Pointer->streams[_selectedStreamIndex];
        
        long timestampTicks = NativeMethods.av_rescale_q
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
        
        NativeMethods.av_seek_frame(ref GetReference(), _selectedStreamIndex, timestampTicks, Constants.AVSEEK_FLAG_BACKWARD | Constants.AVSEEK_FLAG_ANY).ThrowIfError();
    }

    public FFmpegResult ReadFrame(Packet packet)
    {
        ThrowIfDisposedOrUninitialized();

        int returnCode = NativeMethods.av_read_frame(ref GetReference(), ref packet.GetReference())
            .ThrowIfError(rc => rc < 0 &&
                                rc != Constants.AVERROR_EOF &&
                                rc != Constants.AVERROR_EIO &&
                                rc != Constants.Error(Constants.EAGAIN));

        return returnCode switch
        {
            Constants.AVERROR_EOF => FFmpegResult.EndOfStream,
            _ when returnCode == Constants.Error(Constants.EAGAIN) => FFmpegResult.NeedMoreInput,
            Constants.AVERROR_EIO => FFmpegResult.InputOutputError,
            _ when packet.StreamIndex != _selectedStreamIndex => FFmpegResult.Discard,
            _ => FFmpegResult.Success
        };
    }

    protected override void Release(ref AVFormatContext* addressOfPointer)
        => NativeMethods.avformat_close_input(ref addressOfPointer);
}
