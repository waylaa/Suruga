using System.Runtime.InteropServices;
using Suruga.FFmpeg.Loaders;
using Suruga.FFmpeg.Native;
using Suruga.FFmpeg.Primitives;

namespace Suruga.FFmpeg.Interop;

internal sealed unsafe partial class NativeMethods
{
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial AVFrame* av_frame_alloc();
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void av_frame_unref(ref AVFrame frame);

    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void av_frame_free(ref AVFrame* frame);
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void av_channel_layout_default(ref AVChannelLayout ch_layout, int nb_channels);
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial int av_channel_layout_copy(ref AVChannelLayout dst, ref readonly AVChannelLayout src);

    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void av_channel_layout_uninit(ref AVChannelLayout channel_layout);
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial long av_rescale_q(long a, AVRational bq, AVRational cq);

    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial long av_rescale_rnd(long a, long b, long c, AVRounding rnd);

    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void av_log_set_level(int level);

    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void av_log_set_callback(delegate* unmanaged[Cdecl]<void*, int, byte*, byte*, void> callback);
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial int av_log_format_line2(void* ptr, int level, ref readonly byte fmt, ref byte vl, ref byte line, int line_size, ref int print_prefix);
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial AVDictionaryEntry* av_dict_get(ref readonly AVDictionary m, ref readonly byte key, ref readonly AVDictionaryEntry prev, int flags);
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void* av_malloc(nuint size);

    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial void av_freep(void* arg);
    
    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial int av_strerror(int errnum, ref byte errbuf, nuint errbuf_size);

    [LibraryImport(FFmpegLibraryNames.AvUtil)]
    internal static partial byte* av_version_info();
}
