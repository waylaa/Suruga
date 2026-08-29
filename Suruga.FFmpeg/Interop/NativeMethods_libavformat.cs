using System.Runtime.InteropServices;
using Suruga.FFmpeg.Loaders;
using Suruga.FFmpeg.Native;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg.Interop;

internal sealed unsafe partial class NativeMethods
{
    #region AVFormatContext
    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial AVFormatContext* avformat_alloc_context();
    
    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial int avformat_open_input(ref AVFormatContext* ps, ref readonly byte url, ref readonly AVInputFormat fmt, ref AVDictionary* options);

    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial int av_find_best_stream(ref AVFormatContext ic, AVMediaType type, int wanted_stream_nb, int related_stream, ref readonly AVCodec* decoder_ret, int flags);

    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial int avformat_find_stream_info(ref AVFormatContext ic, ref AVDictionary* options);

    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial int av_seek_frame(ref AVFormatContext s, int stream_index, long timestamp, int flags);

    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial int av_read_frame(ref AVFormatContext s, ref AVPacket pkt);
    
    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial void avformat_close_input(ref AVFormatContext* ctx);
    #endregion

    #region AVIOContext
    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial AVIOContext* avio_alloc_context
    (
        ref byte buffer,
        int buffer_size,
        int write_flag,
        void* opaque,
        delegate* unmanaged[Cdecl]<void*, byte*, int, int> read_packet,
        delegate* unmanaged[Cdecl]<void*, byte*, int, int> write_packet,
        delegate* unmanaged[Cdecl]<void*, long, int, long> seek
    );

    [LibraryImport(FFmpegLibraryNames.AvFormat)]
    internal static partial void avio_context_free(ref AVIOContext* s);
    #endregion
}
